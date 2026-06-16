using System.Windows.Controls;
using GameServerManager.Core.Models;
using GameServerManager.App.ViewModels;

namespace GameServerManager.App.Views;

public partial class ArkAsaSettingsView : UserControl
{
    public ArkAsaSettingsView()
    {
        InitializeComponent();
        DataContext = new ArkAsaSettingsViewModel();
    }

    public ArkAsaSettingsView(ServerProfile profile)
    {
        InitializeComponent();
        DataContext = new ArkAsaSettingsViewModel(profile);
    }
}
