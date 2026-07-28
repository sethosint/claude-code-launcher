using ClaudeLauncher.ViewModels;
using System.Windows;

namespace ClaudeLauncher.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(
        SettingsViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        viewModel.RequestClose += saved =>
        {
            try
            {
                DialogResult = saved;
            }
            catch (InvalidOperationException)
            {
                // Window wasn't shown with ShowDialog() (regular Settings
                // menu flow) — DialogResult can't be set in that case, and
                // that's fine, we just close normally below.
            }

            Close();
        };
    }
}