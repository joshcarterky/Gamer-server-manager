using System.Windows;
using System.Windows.Controls;
using GameServerManager.App.ViewModels;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;

namespace GameServerManager.App.Views;

public partial class SevenDaysToDieSettingsView : UserControl
{
    private SevenDaysToDieSettingsViewModel? _vm;

    public SevenDaysToDieSettingsView(ServerProfile profile, IGameServerProvider provider)
    {
        InitializeComponent();
        _vm = new SevenDaysToDieSettingsViewModel(profile, provider);
        DataContext = _vm;

        // Wire PasswordBox controls after the visual tree is ready
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        WirePasswordBoxes(this, _vm);
    }

    // ── PasswordBox two-way binding (WPF PasswordBox doesn't support binding) ──
    //
    // We walk the visual tree, find every PasswordBox whose Tag matches a
    // DtdSettingItemViewModel.Key, and sync bidirectionally on PasswordChanged.

    private static void WirePasswordBoxes(DependencyObject root, SevenDaysToDieSettingsViewModel vm)
    {
        foreach (var item in vm.AllItems.Where(i => i.IsPassword))
        {
            var pwBox = FindPasswordBox(root, item.Key);
            if (pwBox == null) continue;

            // Set initial value
            pwBox.Password = item.Value;

            // UI → VM
            pwBox.PasswordChanged += (_, _) =>
            {
                if (item.Value != pwBox.Password)
                    item.Value = pwBox.Password;
            };

            // VM → UI (in case of revert/reload)
            item.ValueChanged += (_, _) =>
            {
                if (pwBox.Password != item.Value)
                    pwBox.Password = item.Value;
            };
        }
    }

    private static PasswordBox? FindPasswordBox(DependencyObject root, string key)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is PasswordBox pb && pb.Tag?.ToString() == key)
                return pb;
            var found = FindPasswordBox(child, key);
            if (found != null) return found;
        }

        return null;
    }
}
