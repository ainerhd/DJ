namespace AudioMixerController.App;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    public override string ToString() => IsDefault ? $"{Name} (Default)" : Name;
}
