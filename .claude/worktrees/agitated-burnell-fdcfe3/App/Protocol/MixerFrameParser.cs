using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioMixerController.App;

public sealed class MixerFrameParser
{
    private static readonly Regex FrameRegex = new(@"^MIXER,(\d+),(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private long _fallbackSequence;

    public bool TryParse(string line, out MixerFrame? frame)
    {
        frame = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        line = line.Trim();

        var match = FrameRegex.Match(line);
        if (!match.Success)
        {
            return TryParseLegacy(line, out frame);
        }

        if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
        {
            return false;
        }

        var payload = match.Groups[2].Value;
        var parts = payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>(parts.Length);

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            values.Add(value);
        }

        frame = new MixerFrame
        {
            Sequence = sequence,
            RawValues = values,
            RawLine = line
        };

        return true;
    }

    private bool TryParseLegacy(string line, out MixerFrame? frame)
    {
        frame = null;

        var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var values = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            values.Add(value);
        }

        _fallbackSequence++;
        frame = new MixerFrame
        {
            Sequence = _fallbackSequence,
            RawValues = values,
            RawLine = line
        };

        return true;
    }
}
