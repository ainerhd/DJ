namespace AudioMixerController.App;

public sealed class MixerChannelState : BindableBase
{
    private int _rawValue;
    private int _percent;
    private double _filteredPercent;
    private string _channelName = string.Empty;
    private string _targetDeviceName = "Default";
    private double _appliedVolume;
    private bool _isEnabled = true;
    private string _routingStatus = "OK";

    public int ChannelIndex { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(ChannelName) ? $"Kanal {ChannelIndex}" : ChannelName;

    public string ChannelName
    {
        get => _channelName;
        set
        {
            if (SetProperty(ref _channelName, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public int RawValue
    {
        get => _rawValue;
        set => SetProperty(ref _rawValue, value);
    }

    public int Percent
    {
        get => _percent;
        set => SetProperty(ref _percent, value);
    }

    public double FilteredPercent
    {
        get => _filteredPercent;
        set => SetProperty(ref _filteredPercent, value);
    }

    public string TargetDeviceName
    {
        get => _targetDeviceName;
        set => SetProperty(ref _targetDeviceName, value);
    }

    public double AppliedVolume
    {
        get => _appliedVolume;
        set => SetProperty(ref _appliedVolume, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string RoutingStatus
    {
        get => _routingStatus;
        set => SetProperty(ref _routingStatus, value);
    }
}
