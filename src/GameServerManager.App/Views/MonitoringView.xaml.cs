using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class MonitoringView : UserControl
    {
        public MonitoringView()
        {
            InitializeComponent();
            DataContext = new MonitoringViewModel();
        }
    }
}
