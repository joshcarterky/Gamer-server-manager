using System.Windows;
using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private DashboardViewModel? ViewModel => DataContext as DashboardViewModel;

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            ExecuteServerCommand(ViewModel?.StartCommand, sender);
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            ExecuteServerCommand(ViewModel?.StopCommand, sender);
        }

        private void OnRestartClick(object sender, RoutedEventArgs e)
        {
            ExecuteServerCommand(ViewModel?.RestartCommand, sender);
        }

        private void ExecuteServerCommand(System.Windows.Input.ICommand? command, object sender)
        {
            if (command == null)
            {
                return;
            }

            if (sender is not FrameworkElement { DataContext: ServerCardViewModel server })
            {
                return;
            }

            if (command.CanExecute(server))
            {
                command.Execute(server);
            }
        }
    }
}
