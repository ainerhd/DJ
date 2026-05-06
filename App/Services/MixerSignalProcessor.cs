namespace AudioMixerController.App;

public sealed class MixerSignalProcessor
{
    private readonly Dictionary<int, double> _filtered = new();
    private readonly Dictionary<int, Queue<int>> _buffers = new();

    public int AdcMaxValue { get; set; } = 1023;
    public double SmoothingFactor { get; set; } = 0.18;
    public int DeadZone { get; set; } = 2;
    public int BufferSize { get; set; } = 4;

    public (int percent, double filteredPercent) Process(int channelIndex, int rawValue)
    {
        var percent = Normalize(rawValue, AdcMaxValue);
        var buffered = AddAndAverage(channelIndex, percent);

        if (!_filtered.TryGetValue(channelIndex, out var previous))
        {
            previous = buffered;
        }

        if (Math.Abs(previous - buffered) <= DeadZone)
        {
            _filtered[channelIndex] = previous;
            return (percent, previous);
        }

        var filtered = previous + (buffered - previous) * SmoothingFactor;
        _filtered[channelIndex] = filtered;
        return (percent, filtered);
    }

    private double AddAndAverage(int channelIndex, int percent)
    {
        var size = Math.Clamp(BufferSize, 1, 32);
        if (!_buffers.TryGetValue(channelIndex, out var buffer))
        {
            buffer = new Queue<int>(size);
            _buffers[channelIndex] = buffer;
        }

        buffer.Enqueue(percent);
        while (buffer.Count > size)
        {
            buffer.Dequeue();
        }

        return buffer.Average();
    }

    public static int Normalize(int rawValue, int adcMaxValue)
    {
        if (adcMaxValue <= 0)
        {
            return 0;
        }

        var scaled = rawValue / (double)adcMaxValue * 100.0;
        return (int)Math.Round(Math.Clamp(scaled, 0, 100));
    }
}
