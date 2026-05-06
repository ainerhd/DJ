using System.Collections.ObjectModel;
using System.Windows;

namespace AudioMixerController.App;

public sealed class LogStore
{
    public bool Enabled { get; set; }

    public ObservableCollection<string> Lines { get; } = new();

    public void Clear()
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            Lines.Clear();
            return;
        }

        Application.Current?.Dispatcher?.Invoke(() => Lines.Clear());
    }

    public void Add(string message, bool force = false)
    {
        if (!Enabled && !force)
        {
            return;
        }

        var line = $"{DateTime.Now:HH:mm:ss} {message}";

        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            Lines.Add(line);
            return;
        }

        Application.Current?.Dispatcher?.Invoke(() => Lines.Add(line));
    }
}
