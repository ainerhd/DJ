namespace AudioMixerController.App;

public sealed class MixerSignalProcessor
{
    private readonly Dictionary<int, double> _filtered = new();
    private readonly Dictionary<int, Queue<int>> _rawBuffers = new();

    public int AdcMaxValue { get; set; } = 1023;
    public double SmoothingFactor { get; set; } = 0.18;
    public int DeadZone { get; set; } = 2;
    public int BufferSize { get; set; } = 4;

    public (int percent, double filteredPercent) Process(int channelIndex, int rawValue)
    {
        var bufferedRaw = AddAndAverageRaw(channelIndex, rawValue);
        var targetPercent = NormalizeWithEdgeTrim(bufferedRaw, AdcMaxValue, DeadZone);

        if (!_filtered.TryGetValue(channelIndex, out var previous))
        {
            previous = targetPercent;
        }

        double filtered;

        if (targetPercent <= 0.0)
        {
            filtered = 0.0;
        }
        else if (targetPercent >= 100.0)
        {
            filtered = 100.0;
        }
        else
        {
            filtered = previous + (targetPercent - previous) * Math.Clamp(SmoothingFactor, 0.01, 1.0);
            if (Math.Abs(filtered - targetPercent) < 0.1)
            {
                filtered = targetPercent;
            }
        }

        _filtered[channelIndex] = filtered;
        return ((int)Math.Round(targetPercent), filtered);
    }

    private double AddAndAverageRaw(int channelIndex, int raw)
    {
        var size = Math.Clamp(BufferSize, 1, 32);
        if (!_rawBuffers.TryGetValue(channelIndex, out var buffer))
        {
            buffer = new Queue<int>(size);
            _rawBuffers[channelIndex] = buffer;
        }

        buffer.Enqueue(raw);
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

    public static double NormalizeWithEdgeTrim(double rawValue, int adcMaxValue, int edgeTrimRaw)
    {
        if (adcMaxValue <= 0)
        {
            return 0;
        }

        var trim = Math.Clamp(edgeTrimRaw, 0, adcMaxValue / 2);
        var minRaw = trim;
        var maxRaw = adcMaxValue - trim;

        if (rawValue <= minRaw)
        {
            return 0.0;
        }

        if (rawValue >= maxRaw)
        {
            return 100.0;
        }

        var range = maxRaw - minRaw;
        if (range <= 0)
        {
            return rawValue >= adcMaxValue / 2.0 ? 100.0 : 0.0;
        }

        var scaled = (rawValue - minRaw) / range * 100.0;
        return Math.Clamp(scaled, 0.0, 100.0);
    }
}
