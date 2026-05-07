namespace AudioMixerController.App;

public sealed class MixerConfiguration
{
    public string SelectedPort { get; set; } = string.Empty;
    public int SelectedBaudRate { get; set; } = 115200;
    public int ChannelCount { get; set; } = 5;
    public int AdcMaxValue { get; set; } = 1023;
    public double SmoothingFactor { get; set; } = 0.18;
    public int DeadZone { get; set; } = 2;
    public int BufferSize { get; set; } = 4;
    public bool DebugLogsEnabled { get; set; }
    public bool AutoReconnectEnabled { get; set; } = true;
    public double VolumeChangeThreshold { get; set; } = 0.5;
    public int VolumeUpdateIntervalMs { get; set; } = 60;
    public string LastPresetName { get; set; } = "Default";
    public List<MixerChannelMapping> ChannelMappings { get; set; } = new();
}

public sealed class MixerChannelMapping
{
    public int ChannelIndex { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string AudioDeviceId { get; set; } = string.Empty;
    public string AudioDeviceName { get; set; } = "Default";
    public bool Invert { get; set; }
    public bool IsEnabled { get; set; } = true;
}
