using System.Windows;
using System.Windows.Controls;
using GameServerManager.App.ViewModels;
using GameServerManager.Core.Models;
using GameServerManager.Services;

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

        private void OnDeployClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is Shell shell)
                shell.NavigateToDeployPage();
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is Shell shell)
                shell.NavigateToDeployPage();
        }

        // ── Context menu handlers ────────────────────────────────────────────────

        private void OnContextStartClick(object sender, RoutedEventArgs e)
        {
            var server = GetContextMenuServer(sender);
            if (server != null && ViewModel?.StartCommand.CanExecute(server) == true)
                ViewModel.StartCommand.Execute(server);
        }

        private void OnContextStopClick(object sender, RoutedEventArgs e)
        {
            var server = GetContextMenuServer(sender);
            if (server != null && ViewModel?.StopCommand.CanExecute(server) == true)
                ViewModel.StopCommand.Execute(server);
        }

        private void OnContextRestartClick(object sender, RoutedEventArgs e)
        {
            var server = GetContextMenuServer(sender);
            if (server != null && ViewModel?.RestartCommand.CanExecute(server) == true)
                ViewModel.RestartCommand.Execute(server);
        }

        private void OnContextToggleAutoRestartClick(object sender, RoutedEventArgs e)
        {
            var server = GetContextMenuServer(sender);
            if (server == null) return;
            server.AutoRestartOnCrash = !server.AutoRestartOnCrash;
            _ = SaveProfileAsync(server.Profile);
        }

        private void OnContextOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            var server = GetContextMenuServer(sender);
            if (server == null) return;
            if (Application.Current.MainWindow is Shell shell)
                shell.OpenArkAsaSettings(server.Profile);
        }

        private void OnContextOpenConsoleClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is Shell shell)
                shell.NavigateToConsolePage();
        }

        private static ServerCardViewModel? GetContextMenuServer(object sender)
        {
            if (sender is MenuItem mi
                && mi.Parent is ContextMenu cm
                && cm.PlacementTarget is FrameworkElement fe)
                return fe.DataContext as ServerCardViewModel;
            return null;
        }

        private static async Task SaveProfileAsync(ServerProfile profile)
        {
            try
            {
                var paths = new AppDataPaths();
                var service = new ServersJsonService(paths);
                await service.UpdateServerAsync(profile);
            }
            catch { }
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
