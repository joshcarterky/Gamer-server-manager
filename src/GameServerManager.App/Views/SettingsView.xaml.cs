using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }
    }
}
