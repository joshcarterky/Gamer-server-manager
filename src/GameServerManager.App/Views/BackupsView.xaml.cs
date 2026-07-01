using System.Windows.Controls;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views;

public partial class BackupsView : UserControl
{
    public BackupsView()
    {
        InitializeComponent();
        DataContext = new BackupsViewModel();
    }
}
