using System.Windows.Controls;
using GameServerManager.App.ViewModels;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;

namespace GameServerManager.App.Views;

public partial class GenericServerSettingsView : UserControl
{
    public GenericServerSettingsView(ServerProfile profile, IGameServerProvider provider)
    {
        InitializeComponent();
        DataContext = new GenericServerSettingsViewModel(profile, provider);
    }
}
