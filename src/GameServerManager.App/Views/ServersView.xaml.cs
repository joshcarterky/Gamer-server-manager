using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class ServersView : UserControl
    {
        public ServersView()
        {
            InitializeComponent();
            var vm = new ServersViewModel();
            DataContext = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ServersViewModel.InstallLogText))
            {
                Dispatcher.InvokeAsync(() => InstallLogScroll.ScrollToEnd(), DispatcherPriority.Background);
            }
        }

        private void OnPowerMenuClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
                element.ContextMenu.IsOpen = true;
        }
    }
}
