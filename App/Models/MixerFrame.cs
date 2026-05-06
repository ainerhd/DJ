namespace AudioMixerController.App;

public sealed class MixerFrame
{
    public long Sequence { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<int> RawValues { get; init; } = Array.Empty<int>();
    public string RawLine { get; init; } = string.Empty;
}
