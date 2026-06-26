using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views;

public partial class DeployView : UserControl
{
    private DeployViewModel Vm => (DeployViewModel)DataContext;

    public DeployView()
    {
        InitializeComponent();
        DataContext = new DeployViewModel();

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Vm.InstallLog.CollectionChanged += OnInstallLogChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Vm.InstallLog.CollectionChanged -= OnInstallLogChanged;
        Vm.Dispose();
    }

    // Auto-scroll the install log as new lines arrive
    private void OnInstallLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.InvokeAsync(
                () => InstallScroller.ScrollToEnd(),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
