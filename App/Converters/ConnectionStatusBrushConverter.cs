using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AudioMixerController.App;

public sealed class ConnectionStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? string.Empty;

        if (status.Contains("Connected", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Color.FromRgb(87, 217, 149));
        }

        if (status.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("No COM", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Color.FromRgb(224, 92, 92));
        }

        if (status.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("init", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Color.FromRgb(245, 166, 35));
        }

        return new SolidColorBrush(Color.FromRgb(110, 118, 129));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
