using System.Drawing;
using H.NotifyIcon;

namespace GameServerManager.App;

public enum TrayBalloonIcon { Info, Warning, Error }

internal static class TrayService
{
    private static TaskbarIcon? _icon;
    private static System.Windows.Window? _mainWindow;

    public static bool IsExiting { get; private set; }

    public static void Initialize(System.Windows.Window mainWindow)
    {
        _mainWindow = mainWindow;

        Icon trayIcon;
        try
        {
            var res = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/AppIcon.ico"));
            trayIcon = res?.Stream != null ? new Icon(res.Stream) : SystemIcons.Application;
        }
        catch
        {
            trayIcon = SystemIcons.Application;
        }

        var menu = new System.Windows.Controls.ContextMenu();
        AddMenuItem(menu, "Open Nexus Server Manager", (_, _) => ShowWindow());
        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "Exit", (_, _) => ExitApp());

        _icon = new TaskbarIcon
        {
            Icon = trayIcon,
            ToolTipText = "Nexus Server Manager",
            ContextMenu = menu
        };
        _icon.TrayMouseDoubleClick += (_, _) => ShowWindow();
    }

    public static void HideToTray()
    {
        _mainWindow?.Hide();
    }

    public static void ShowWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.Activate();
    }

    public static void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info)
    {
        if (_icon == null) return;
        try
        {
            var notifIcon = icon switch
            {
                TrayBalloonIcon.Warning => H.NotifyIcon.Core.NotificationIcon.Warning,
                TrayBalloonIcon.Error => H.NotifyIcon.Core.NotificationIcon.Error,
                _ => H.NotifyIcon.Core.NotificationIcon.Info
            };
            _icon.ShowNotification(title, message, notifIcon);
        }
        catch
        {
            // Balloon tips are best-effort
        }
    }

    public static void Dispose()
    {
        if (_icon != null)
        {
            _icon.Dispose();
            _icon = null;
        }
    }

    private static void ExitApp()
    {
        IsExiting = true;
        Dispose();
        System.Windows.Application.Current.Dispatcher.Invoke(
            () => System.Windows.Application.Current.Shutdown());
    }

    private static void AddMenuItem(System.Windows.Controls.ContextMenu menu, string header,
        System.Windows.RoutedEventHandler handler)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += handler;
        menu.Items.Add(item);
    }
}
