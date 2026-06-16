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
    }
}
