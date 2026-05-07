namespace AudioMixerController.App;

public sealed class ChannelRoutingRow : BindableBase
{
    private string _audioDeviceId = string.Empty;
    private bool _invert;

    public int ChannelIndex { get; init; }

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
}
