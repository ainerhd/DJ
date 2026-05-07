namespace AudioMixerController.App;

public sealed class ChannelRoutingRow : BindableBase
{
    private string _channelName = string.Empty;
    private string _audioDeviceId = string.Empty;
    private bool _invert;
    private bool _isEnabled = true;
    private int? _calibrationMinRaw;
    private int? _calibrationMaxRaw;

    public int ChannelIndex { get; init; }

    public string ChannelName
    {
        get => _channelName;
        set => SetProperty(ref _channelName, value);
    }

    public string AudioDeviceId
    {
        get => _audioDeviceId;
        set => SetProperty(ref _audioDeviceId, value);
    }

    public bool Invert
    {
        get => _invert;
        set => SetProperty(ref _invert, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int? CalibrationMinRaw
    {
        get => _calibrationMinRaw;
        set => SetProperty(ref _calibrationMinRaw, value);
    }

    public int? CalibrationMaxRaw
    {
        get => _calibrationMaxRaw;
        set => SetProperty(ref _calibrationMaxRaw, value);
    }
}
