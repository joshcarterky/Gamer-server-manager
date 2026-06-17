using System.Windows;
using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class ServersView : UserControl
    {
        public ServersView()
        {
            InitializeComponent();
            DataContext = new ServersViewModel();
        }

        private void OnPowerMenuClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
                element.ContextMenu.IsOpen = true;
        }

        private void OnMoreMenuClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
                element.ContextMenu.IsOpen = true;
        }
    }
}
