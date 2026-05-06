namespace AudioMixerController.App;

public sealed class MixerPreset
{
    public string Name { get; set; } = "Default";
    public MixerConfiguration Configuration { get; set; } = new();
}
