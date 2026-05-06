using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AudioMixerController.App;

public sealed class MainViewModel : BindableBase, IDisposable
{
    private readonly SerialMixerService _serial;
    private readonly IAudioVolumeService _audio;
    private readonly SettingsStore _settings;
    private readonly MixerSignalProcessor _processor = new();
    private MixerConfiguration _configuration = new();
    private DateTime _fpsWindowStart = DateTime.UtcNow;
    private int _fpsCounter;
    private int _currentFps;

    private Dictionary<int, ChannelRoutingRow> _routeCache = new();
    private Dictionary<string, AudioDeviceInfo> _deviceCache = new();

    private string _selectedPort = string.Empty;
    private int _selectedBaudRate = 115200;
    private string _connectionStatus = "Disconnected";
    private string _activePort = "-";
    private string _selectedPresetName = "Default";
    private int _deadZone = 2;
    private int _bufferSize = 4;
    private double _smoothingFactor = 0.18;
    private bool _debugLogsEnabled;

    public MainViewModel(SerialMixerService serial, IAudioVolumeService audio, SettingsStore settings, LogStore logStore)
    {
        _serial = serial;
        _audio = audio;
        _settings = settings;
        LogStore = logStore;

        AvailablePorts = new ObservableCollection<string>();
        AvailableBaudRates = new ObservableCollection<int>(new[] { 9600, 115200, 230400 });
        Channels = new ObservableCollection<MixerChannelState>();
        PresetNames = new ObservableCollection<string>();
        AudioDevices = new ObservableCollection<AudioDeviceInfo>();
        ChannelRouting = new ObservableCollection<ChannelRoutingRow>();
        LogLines = logStore.Lines;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !string.IsNullOrWhiteSpace(SelectedPort));
        DisconnectCommand = new RelayCommand(Disconnect);
        RefreshDevicesCommand = new RelayCommand(RefreshAudioDevices);
        SavePresetCommand = new RelayCommand(async () => await SavePresetAsync());
        LoadPresetCommand = new RelayCommand(async () => await LoadPresetAsync());
        SaveSettingsCommand = new RelayCommand(async () => await SaveSettingsAsync());
        ClearLogsCommand = new RelayCommand(ClearLogs);

        _serial.FrameReceived += OnFrameReceived;
        _serial.StatusChanged += status => Application.Current.Dispatcher.InvokeAsync(() => ConnectionStatus = status);
    }

    public LogStore LogStore { get; }
    public ObservableCollection<string> AvailablePorts { get; }
    public ObservableCollection<int> AvailableBaudRates { get; }
    public ObservableCollection<MixerChannelState> Channels { get; }
    public ObservableCollection<string> PresetNames { get; }
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; }
    public ObservableCollection<ChannelRoutingRow> ChannelRouting { get; }
    public ObservableCollection<string> LogLines { get; }

    public ICommand RefreshPortsCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ClearLogsCommand { get; }

    public string SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                (ConnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string ActivePort
    {
        get => _activePort;
        set => SetProperty(ref _activePort, value);
    }

    public int CurrentFps
    {
        get => _currentFps;
        set => SetProperty(ref _currentFps, value);
    }

    public string SelectedPresetName
    {
        get => _selectedPresetName;
        set => SetProperty(ref _selectedPresetName, value);
    }

    public int DeadZone
    {
        get => _deadZone;
        set
        {
            if (SetProperty(ref _deadZone, value))
            {
                _processor.DeadZone = Math.Max(0, value);
            }
        }
    }

    public int BufferSize
    {
        get => _bufferSize;
        set
        {
            if (SetProperty(ref _bufferSize, value))
            {
                _processor.BufferSize = Math.Clamp(value, 1, 32);
            }
        }
    }

    public double SmoothingFactor
    {
        get => _smoothingFactor;
        set
        {
            if (SetProperty(ref _smoothingFactor, value))
            {
                _processor.SmoothingFactor = Math.Clamp(value, 0.01, 1.0);
            }
        }
    }

    public bool DebugLogsEnabled
    {
        get => _debugLogsEnabled;
        set
        {
            if (SetProperty(ref _debugLogsEnabled, value))
            {
                LogStore.Enabled = value;
            }
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _configuration = await _settings.LoadAsync();
        }
        catch (Exception ex)
        {
            _configuration = new MixerConfiguration();
            LogStore.Add($"Settings load error: {ex.Message}", true);
        }

        ApplyConfiguration();

        RefreshPorts();
        RefreshPresets();
        try
        {
            RefreshAudioDevices();
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Audio init failed";
            LogStore.Add($"Audio device init error: {ex.Message}", true);
        }
        RebuildChannels(_configuration.ChannelCount);
        RebuildChannelRouting();
    }

    private void ApplyConfiguration()
    {
        SelectedPort = _configuration.SelectedPort;
        SelectedBaudRate = _configuration.SelectedBaudRate;
        DeadZone = _configuration.DeadZone;
        BufferSize = _configuration.BufferSize;
        SmoothingFactor = _configuration.SmoothingFactor;
        DebugLogsEnabled = _configuration.DebugLogsEnabled;
        _processor.AdcMaxValue = _configuration.AdcMaxValue;
    }

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in _serial.GetPortNames())
        {
            AvailablePorts.Add(port);
        }

        if (string.IsNullOrWhiteSpace(SelectedPort) && AvailablePorts.Count > 0)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    private void RefreshAudioDevices()
    {
        AudioDevices.Clear();
        AudioDevices.Add(new AudioDeviceInfo { Id = string.Empty, Name = "Default Device", IsDefault = true });

        foreach (var device in _audio.GetOutputDevices())
        {
            AudioDevices.Add(device);
        }

        _deviceCache = AudioDevices.ToDictionary(d => d.Id);
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            ConnectionStatus = "No COM selected";
            return;
        }

        try
        {
            await _serial.ConnectAsync(SelectedPort, SelectedBaudRate);
            ActivePort = SelectedPort;
            ConnectionStatus = $"Connected ({SelectedBaudRate})";
            await SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Connect failed";
            LogStore.Add($"Connect error: {ex.Message}", true);
        }
    }

    private void Disconnect()
    {
        _serial.Disconnect();
        ActivePort = "-";
        ConnectionStatus = "Disconnected";
    }

    private async Task SaveSettingsAsync()
    {
        CaptureConfigurationFromUi();
        await _settings.SaveAsync(_configuration);
    }

    private async Task SavePresetAsync()
    {
        CaptureConfigurationFromUi();

        var preset = new MixerPreset
        {
            Name = string.IsNullOrWhiteSpace(SelectedPresetName) ? "Default" : SelectedPresetName,
            Configuration = _configuration
        };

        await _settings.SavePresetAsync(preset);
        RefreshPresets();
    }

    private async Task LoadPresetAsync()
    {
        var preset = await _settings.LoadPresetAsync(SelectedPresetName);
        if (preset is null)
        {
            LogStore.Add($"Preset missing: {SelectedPresetName}", true);
            return;
        }

        _configuration = preset.Configuration;
        ApplyConfiguration();
        RebuildChannels(_configuration.ChannelCount);
        RebuildChannelRouting();
    }

    private void RefreshPresets()
    {
        PresetNames.Clear();
        foreach (var presetName in _settings.GetPresetNames())
        {
            PresetNames.Add(presetName);
        }

        if (PresetNames.Count > 0 && !PresetNames.Contains(SelectedPresetName))
        {
            SelectedPresetName = PresetNames[0];
        }
    }

    private void RebuildChannels(int count)
    {
        Channels.Clear();
        for (var i = 0; i < count; i++)
        {
            Channels.Add(new MixerChannelState
            {
                ChannelIndex = i + 1,
                TargetDeviceName = "Default Device"
            });
        }
    }

    private void RebuildChannelRouting()
    {
        ChannelRouting.Clear();

        EnsureMappingsForChannelCount();

        for (var i = 0; i < _configuration.ChannelCount; i++)
        {
            var map = _configuration.ChannelMappings.First(x => x.ChannelIndex == i + 1);
            ChannelRouting.Add(new ChannelRoutingRow
            {
                ChannelIndex = map.ChannelIndex,
                AudioDeviceId = map.AudioDeviceId,
                Invert = map.Invert
            });
        }

        _routeCache = ChannelRouting.ToDictionary(r => r.ChannelIndex);
    }

    private void EnsureMappingsForChannelCount()
    {
        for (var i = 1; i <= _configuration.ChannelCount; i++)
        {
            if (_configuration.ChannelMappings.All(x => x.ChannelIndex != i))
            {
                _configuration.ChannelMappings.Add(new MixerChannelMapping
                {
                    ChannelIndex = i,
                    AudioDeviceId = string.Empty,
                    AudioDeviceName = "Default Device"
                });
            }
        }

        _configuration.ChannelMappings = _configuration.ChannelMappings
            .Where(x => x.ChannelIndex <= _configuration.ChannelCount)
            .OrderBy(x => x.ChannelIndex)
            .ToList();
    }

    private void CaptureConfigurationFromUi()
    {
        _configuration.SelectedPort = SelectedPort;
        _configuration.SelectedBaudRate = SelectedBaudRate;
        _configuration.DeadZone = DeadZone;
        _configuration.BufferSize = BufferSize;
        _configuration.SmoothingFactor = SmoothingFactor;
        _configuration.DebugLogsEnabled = DebugLogsEnabled;

        _configuration.ChannelMappings = ChannelRouting.Select(row =>
        {
            var device = _deviceCache.TryGetValue(row.AudioDeviceId, out var d) ? d : null;
            return new MixerChannelMapping
            {
                ChannelIndex = row.ChannelIndex,
                AudioDeviceId = row.AudioDeviceId,
                AudioDeviceName = device?.Name ?? "Default Device",
                Invert = row.Invert
            };
        }).OrderBy(x => x.ChannelIndex).ToList();
    }

    private void OnFrameReceived(MixerFrame frame)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.InvokeAsync(() => OnFrameReceived(frame));
            return;
        }

        _fpsCounter++;
        if ((DateTime.UtcNow - _fpsWindowStart).TotalSeconds >= 1)
        {
            _fpsWindowStart = DateTime.UtcNow;
            CurrentFps = _fpsCounter;
            _fpsCounter = 0;
        }

        for (var i = 0; i < frame.RawValues.Count && i < Channels.Count; i++)
        {
            var channel = Channels[i];
            var raw = frame.RawValues[i];
            var (percent, filtered) = _processor.Process(i, raw);

            channel.RawValue = raw;
            channel.Percent = percent;
            channel.FilteredPercent = Math.Round(filtered, 1);

            _routeCache.TryGetValue(i + 1, out var route);
            var deviceId = route?.AudioDeviceId ?? string.Empty;
            var invert = route?.Invert ?? false;

            var volume = invert ? 100.0 - filtered : filtered;
            channel.AppliedVolume = Math.Round(volume, 1);
            channel.TargetDeviceName = _deviceCache.TryGetValue(deviceId, out var dev) ? dev.Name : "Default Device";

            try
            {
                _audio.SetMasterVolume(deviceId, volume);
            }
            catch (Exception ex)
            {
                LogStore.Add($"Audio error ch{channel.ChannelIndex}: {ex.Message}", true);
            }
        }
    }

    private void ClearLogs() => LogStore.Clear();

    public void Dispose() => _serial.Dispose();
}
