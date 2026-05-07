using System.Windows;

namespace AudioMixerController.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private MonitorWindow? _monitorWindow;

    public MainWindow()
    {
        InitializeComponent();

        var logStore = new LogStore();
        var serialService = new SerialMixerService(logStore);
        var audioService = new WindowsAudioVolumeService(logStore);
        var settingsStore = new SettingsStore();
        _viewModel = new MainViewModel(serialService, audioService, settingsStore, logStore);
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}", "Audio Mixer Controller", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OpenMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (_monitorWindow is null || !_monitorWindow.IsVisible)
        {
            _monitorWindow = new MonitorWindow(_viewModel)
            {
                Owner = this
            };
            _monitorWindow.Show();
            return;
        }

        _monitorWindow.Activate();
    }
}
