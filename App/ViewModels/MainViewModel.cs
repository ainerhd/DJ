using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
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
    private CancellationTokenSource? _reconnectCts;
    private readonly object _latestLock = new();
    private int[] _latestRaw = Array.Empty<int>();
    private int[] _latestPercent = Array.Empty<int>();
    private double[] _latestFiltered = Array.Empty<double>();
    private double[] _latestApplied = Array.Empty<double>();
    private string[] _latestDeviceNames = Array.Empty<string>();
    private long _lastUiPushTicks;
    private int _uiPushPending;

    private Dictionary<int, ChannelRoutingRow> _routeCache = new();
    private Dictionary<string, AudioDeviceInfo> _deviceCache = new();
    private readonly Dictionary<int, double> _lastVolumeByChannel = new();
    private readonly Dictionary<int, long> _lastVolumeTicksByChannel = new();

    private string _selectedPort = string.Empty;
    private int _selectedBaudRate = 115200;
    private string _connectionStatus = "Disconnected";
    private string _activePort = "-";
    private string _selectedPresetName = "Default";
    private string _presetNameInput = "Default";
    private int _deadZone = 2;
    private int _bufferSize = 4;
    private double _smoothingFactor = 0.18;
    private bool _debugLogsEnabled;
    private bool _autoReconnectEnabled = true;
    private bool _shouldKeepConnected;

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
        RefreshDevicesCommand = new RelayCommand(RefreshAudioDevicesSafe);
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

    public string SelectedPresetName
    {
        get => _selectedPresetName;
        set
        {
            if (SetProperty(ref _selectedPresetName, value))
            {
                PresetNameInput = value;
            }
        }
    }

    public string PresetNameInput
    {
        get => _presetNameInput;
        set => SetProperty(ref _presetNameInput, value);
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

    public bool AutoReconnectEnabled
    {
        get => _autoReconnectEnabled;
        set => SetProperty(ref _autoReconnectEnabled, value);
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
        StartReconnectLoop();

        if (!string.IsNullOrWhiteSpace(_configuration.LastPresetName) && PresetNames.Contains(_configuration.LastPresetName))
        {
            SelectedPresetName = _configuration.LastPresetName;
            await LoadPresetAsync();
        }

        if (!string.IsNullOrWhiteSpace(SelectedPort) && AvailablePorts.Contains(SelectedPort))
        {
            await ConnectAsync();
        }
    }

    private void ApplyConfiguration()
    {
        SelectedPort = _configuration.SelectedPort;
        SelectedBaudRate = _configuration.SelectedBaudRate;
        SelectedPresetName = string.IsNullOrWhiteSpace(_configuration.LastPresetName) ? "Default" : _configuration.LastPresetName;
        PresetNameInput = SelectedPresetName;
        DeadZone = _configuration.DeadZone;
        BufferSize = _configuration.BufferSize;
        SmoothingFactor = _configuration.SmoothingFactor;
        DebugLogsEnabled = _configuration.DebugLogsEnabled;
        AutoReconnectEnabled = _configuration.AutoReconnectEnabled;
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

    private void RefreshAudioDevicesSafe()
    {
        try
        {
            RefreshAudioDevices();
        }
        catch (Exception ex)
        {
            LogStore.Add($"Refresh devices error: {ex.Message}", true);
            ConnectionStatus = "Audio device load failed";
        }
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
            _shouldKeepConnected = true;
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
        _shouldKeepConnected = false;
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

        var targetName = string.IsNullOrWhiteSpace(PresetNameInput) ? SelectedPresetName : PresetNameInput.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetName = "Default";
        }

        var preset = new MixerPreset
        {
            Name = targetName,
            Configuration = _configuration
        };

        await _settings.SavePresetAsync(preset);
        _configuration.LastPresetName = targetName;
        SelectedPresetName = targetName;
        await _settings.SaveAsync(_configuration);
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
        _configuration.LastPresetName = preset.Name;
        SelectedPresetName = preset.Name;
        ApplyConfiguration();
        RebuildChannels(_configuration.ChannelCount);
        RebuildChannelRouting();
        await _settings.SaveAsync(_configuration);
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
                ChannelName = string.IsNullOrWhiteSpace(map.ChannelName) ? $"Kanal {map.ChannelIndex}" : map.ChannelName,
                AudioDeviceId = map.AudioDeviceId,
                Invert = map.Invert,
                IsEnabled = map.IsEnabled
            });
        }

        _routeCache = ChannelRouting.ToDictionary(r => r.ChannelIndex);

        foreach (var row in ChannelRouting)
        {
            row.PropertyChanged += (_, _) => _routeCache = ChannelRouting.ToDictionary(r => r.ChannelIndex);
        }
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
                    ChannelName = $"Kanal {i}",
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
        _configuration.AutoReconnectEnabled = AutoReconnectEnabled;
        _configuration.LastPresetName = string.IsNullOrWhiteSpace(SelectedPresetName) ? "Default" : SelectedPresetName;

        _configuration.ChannelMappings = ChannelRouting.Select(row =>
        {
            var device = _deviceCache.TryGetValue(row.AudioDeviceId, out var d) ? d : null;
            return new MixerChannelMapping
            {
                ChannelIndex = row.ChannelIndex,
                ChannelName = row.ChannelName,
                AudioDeviceId = row.AudioDeviceId,
                AudioDeviceName = device?.Name ?? "Default Device",
                Invert = row.Invert,
                IsEnabled = row.IsEnabled
            };
        }).OrderBy(x => x.ChannelIndex).ToList();
    }

    private void OnFrameReceived(MixerFrame frame)
    {
        var channelCount = Math.Min(frame.RawValues.Count, Channels.Count);
        if (channelCount <= 0)
        {
            return;
        }

        var raw = new int[channelCount];
        var percent = new int[channelCount];
        var filtered = new double[channelCount];
        var applied = new double[channelCount];
        var deviceNames = new string[channelCount];

        for (var i = 0; i < channelCount; i++)
        {
            raw[i] = frame.RawValues[i];
            var result = _processor.Process(i, raw[i]);
            percent[i] = result.percent;
            filtered[i] = Math.Round(result.filteredPercent, 1);

            _routeCache.TryGetValue(i + 1, out var route);
            var enabled = route?.IsEnabled ?? true;
            var deviceId = route?.AudioDeviceId ?? string.Empty;
            var invert = route?.Invert ?? false;

            var volume = invert ? 100.0 - result.filteredPercent : result.filteredPercent;
            applied[i] = Math.Round(volume, 1);
            var deviceExists = string.IsNullOrWhiteSpace(deviceId) || _deviceCache.ContainsKey(deviceId);
            deviceNames[i] = !enabled
                ? "Inaktiv"
                : _deviceCache.TryGetValue(deviceId, out var dev)
                    ? dev.Name
                    : string.IsNullOrWhiteSpace(deviceId) ? "Default Device" : "Gerät fehlt";

            if (!enabled || !deviceExists || !ShouldSendVolume(i, volume))
            {
                continue;
            }

            try
            {
                _audio.SetMasterVolume(deviceId, volume);
            }
            catch (Exception ex)
            {
                LogStore.Add($"Audio error ch{i + 1}: {ex.Message}", true);
            }
        }

        lock (_latestLock)
        {
            _latestRaw = raw;
            _latestPercent = percent;
            _latestFiltered = filtered;
            _latestApplied = applied;
            _latestDeviceNames = deviceNames;
        }

        var now = Stopwatch.GetTimestamp();
        var elapsedMs = (now - _lastUiPushTicks) * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs < 50)
        {
            return;
        }

        _lastUiPushTicks = now;
        if (Interlocked.Exchange(ref _uiPushPending, 1) == 1)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                int[] latestRaw;
                int[] latestPercent;
                double[] latestFiltered;
                double[] latestApplied;
                string[] latestDeviceNames;

                lock (_latestLock)
                {
                    latestRaw = _latestRaw;
                    latestPercent = _latestPercent;
                    latestFiltered = _latestFiltered;
                    latestApplied = _latestApplied;
                    latestDeviceNames = _latestDeviceNames;
                }

                var count = Math.Min(Channels.Count, latestRaw.Length);
                for (var i = 0; i < count; i++)
                {
                    var channel = Channels[i];
                    channel.RawValue = latestRaw[i];
                    channel.Percent = latestPercent[i];
                    channel.FilteredPercent = latestFiltered[i];
                    channel.AppliedVolume = latestApplied[i];
                    channel.TargetDeviceName = latestDeviceNames[i];
                    if (_routeCache.TryGetValue(i + 1, out var route))
                    {
                        channel.ChannelName = route.ChannelName;
                        channel.IsEnabled = route.IsEnabled;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _uiPushPending, 0);
            }
        });
    }

    private void ClearLogs() => LogStore.Clear();

    private bool ShouldSendVolume(int channelIndex, double volume)
    {
        var now = Stopwatch.GetTimestamp();
        var intervalMs = Math.Max(10, _configuration.VolumeUpdateIntervalMs);
        var threshold = Math.Max(0.0, _configuration.VolumeChangeThreshold);

        _lastVolumeByChannel.TryGetValue(channelIndex, out var previousVolume);
        _lastVolumeTicksByChannel.TryGetValue(channelIndex, out var previousTicks);

        var elapsedMs = previousTicks == 0 ? double.MaxValue : (now - previousTicks) * 1000.0 / Stopwatch.Frequency;
        var isHardEdge = volume <= 0.0 || volume >= 100.0;
        var changedEnough = Math.Abs(volume - previousVolume) >= threshold;

        if (!isHardEdge && !changedEnough && elapsedMs < intervalMs)
        {
            return false;
        }

        _lastVolumeByChannel[channelIndex] = volume;
        _lastVolumeTicksByChannel[channelIndex] = now;
        return true;
    }

    private void StartReconnectLoop()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        _ = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
    }

    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token);

                if (!AutoReconnectEnabled || !_shouldKeepConnected || _serial.IsConnected || string.IsNullOrWhiteSpace(SelectedPort))
                {
                    continue;
                }

                var ports = _serial.GetPortNames();
                if (!ports.Contains(SelectedPort))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => ConnectionStatus = "Waiting for COM");
                    continue;
                }

                await Application.Current.Dispatcher.InvokeAsync(() => ConnectionStatus = "Reconnecting...");
                await ConnectAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogStore.Add($"Reconnect error: {ex.Message}", true);
            }
        }
    }

    public void Dispose()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _serial.Dispose();
    }
}
