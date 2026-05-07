namespace AudioMixerController.App;

public sealed class MixerChannelState : BindableBase
{
    private int _rawValue;
    private int _percent;
    private double _filteredPercent;
    private string _targetDeviceName = "Default";
    private double _appliedVolume;

    public int ChannelIndex { get; init; }

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
}
