using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class SchedulerView : UserControl
    {
        public SchedulerView()
        {
            InitializeComponent();
            DataContext = new SchedulerViewModel();
        }
    }
}
