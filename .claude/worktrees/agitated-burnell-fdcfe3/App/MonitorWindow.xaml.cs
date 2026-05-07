using System.Windows;

namespace AudioMixerController.App;

public partial class MonitorWindow : Window
{
    public MonitorWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
