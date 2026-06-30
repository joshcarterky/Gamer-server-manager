using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GameServerManager.Core.Models;
using GameServerManager.App.ViewModels;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.Updates;

namespace GameServerManager.App.Views
{
    public partial class Shell : Window
    {
        private readonly Brush _activeBackground = new SolidColorBrush(Color.FromRgb(53, 74, 94));
        private readonly Brush _inactiveBackground = Brushes.Transparent;
        private readonly Brush _activeForeground = Brushes.White;
        private readonly Brush _inactiveForeground = new SolidColorBrush(Color.FromRgb(206, 217, 232));

        private readonly DispatcherTimer _statusTimer;
        private readonly DispatcherTimer _schedulerTimer;

        public Shell()
        {
            InitializeComponent();
            _ = LoadSettingsAsync();
            _ = CheckForUpdateBannerAsync();
            NavigateTo("Dashboard", DashboardButton);

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _statusTimer.Tick += async (_, _) => await UpdateStatusBarAsync();
            _statusTimer.Start();
            _ = UpdateStatusBarAsync();

            TrayService.Initialize(this);

            _schedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _schedulerTimer.Tick += async (_, _) => await RunScheduledTasksAsync();
            _schedulerTimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!TrayService.IsExiting)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            _statusTimer.Stop();
            _schedulerTimer.Stop();
            TrayService.Dispose();
            base.OnClosing(e);
        }

        private async Task RunScheduledTasksAsync()
        {
            try
            {
                var paths = new AppDataPaths();
                var scheduler = new SchedulerService(paths);
                var serversService = new ServersJsonService(paths);
                var tasks = await scheduler.LoadAsync();
                var profiles = await serversService.LoadServersAsync();
                var now = DateTime.UtcNow;
                var changed = false;

                foreach (var task in tasks.Where(t => t.Enabled))
                {
                    var profile = profiles.FirstOrDefault(p => p.Id == task.ServerId);
                    if (profile == null) continue;

                    // Pre-warning for restart
                    if (task.TaskType == ScheduledTaskType.Restart && SchedulerService.IsPreWarnDue(task, now))
                    {
                        var msg = $"Server restarting in {task.WarnMinutesBefore} minute{(task.WarnMinutesBefore == 1 ? "" : "s")}!";
                        _ = SendRconCommandAsync(profile, $"Broadcast {msg}");
                        task.LastPreWarnAt = now;
                        changed = true;
                    }

                    if (!SchedulerService.IsDue(task, now)) continue;

                    var taskLabel = task.TaskType switch
                    {
                        ScheduledTaskType.Restart => "Restart",
                        ScheduledTaskType.Broadcast => "Broadcast",
                        ScheduledTaskType.DinoWipe => "Dino Wipe",
                        _ => "Custom RCON"
                    };

                    try
                    {
                        switch (task.TaskType)
                        {
                            case ScheduledTaskType.Restart:
                                var registry = GameProviderRegistry.CreateDefault();
                                var procService = new ServerProcessService(registry, paths);
                                await procService.RestartServerAsync(profile);
                                await serversService.UpdateServerAsync(profile);
                                procService.Dispose();
                                TrayService.ShowBalloon("Scheduled Restart", $"{profile.ProfileName} restarted.");
                                break;
                            case ScheduledTaskType.Broadcast:
                                await SendRconCommandAsync(profile, $"Broadcast {task.Parameter}");
                                break;
                            case ScheduledTaskType.DinoWipe:
                                await SendRconCommandAsync(profile, "destroywilddinos");
                                break;
                            case ScheduledTaskType.CustomRcon:
                                await SendRconCommandAsync(profile, task.Parameter);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        TrayService.ShowBalloon("Scheduler Error", $"{taskLabel}: {ex.Message}", TrayBalloonIcon.Error);
                    }

                    task.LastRunAt = now;
                    task.NextRunAt = SchedulerService.ComputeNextRun(task, now);
                    task.LastPreWarnAt = null;
                    changed = true;
                }

                if (changed)
                    await scheduler.SaveAsync(tasks);
            }
            catch
            {
                // Scheduler failures are silent
            }
        }

        private static async Task SendRconCommandAsync(ServerProfile profile, string command)
        {
            var rconPort = profile.Ports
                .FirstOrDefault(p => p.Name.Equals("RCON", StringComparison.OrdinalIgnoreCase))?.Port ?? 0;
            if (rconPort == 0) return;

            var password = profile.Settings.TryGetValue("RCONPassword", out var pwd) && !string.IsNullOrWhiteSpace(pwd)
                ? pwd
                : profile.AdminPassword;

            using var rcon = new SourceRconClient();
            var connected = await rcon.ConnectAsync("127.0.0.1", rconPort, password);
            if (connected)
                await rcon.ExecAsync(command);
        }

        public void ApplySettings(AppSettings settings)
        {
            var applicationName = string.IsNullOrWhiteSpace(settings.ApplicationName)
                ? "Game Server Manager"
                : settings.ApplicationName.Trim();

            Title = applicationName;
            BrandNameText.Text = applicationName;
            ShellFrame.Background = CreateThemeBrush(settings.Theme);
            ShellFrame.CornerRadius = new CornerRadius(Clamp(settings.CornerRadius, 0, 24));
            SidebarColumn.Width = new GridLength(Clamp(settings.SidebarWidth, 180, 300));
            HeaderRow.Height = new GridLength(settings.CompactHeader ? 46 : 58);
            LogoAccent.Background = CreateAccentBrush(settings.AccentColor);
            ApplyDensity(settings.Density);

            var intensity = Clamp(settings.BackgroundIntensity, 40, 100);
            var sidebarAlpha = settings.GlassPanels ? (byte)(90 + intensity) : (byte)255;
            var headerAlpha = settings.GlassPanels ? (byte)(70 + intensity) : (byte)255;
            SidebarPanel.Background = new SolidColorBrush(Color.FromArgb(sidebarAlpha, 17, 27, 43));
            HeaderPanel.Background = new SolidColorBrush(Color.FromArgb(headerAlpha, 16, 37, 54));
        }

        private void OnShellKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                if (MainContentArea.Content is DashboardView { DataContext: DashboardViewModel dashVM })
                    dashVM.RefreshCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnNavigationClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pageName)
            {
                NavigateTo(pageName, button);
            }
        }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnViewUpdatesClick(object sender, RoutedEventArgs e)
        {
            NavigateTo("Settings", SettingsButton);
        }

        private void OnDismissUpdateBannerClick(object sender, RoutedEventArgs e)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                ShellFrame.Margin = new Thickness(6);
                MaximizeButton.Content = "[]]";
            }
            else
            {
                ShellFrame.Margin = new Thickness(0);
                MaximizeButton.Content = "[ ]";
            }
        }

        private void NavigateTo(string pageName, Button activeButton)
        {
            PageTitleText.Text = pageName;
            SetActiveButton(activeButton);

            MainContentArea.Content = pageName switch
            {
                "Dashboard" => new DashboardView { DataContext = new DashboardViewModel() },
                "Servers" => new ServersView(),
                "Settings" => new SettingsView(),
                "File Manager" => new FileManagerView(),
                "Console" => new ConsoleView(),
                "Deploy" => new DeployView(),
                "Monitoring" => new MonitoringView(),
                "Scheduler" => new SchedulerView(),
                _ => CreatePlaceholderPage(pageName)
            };
        }

        public void NavigateToConsolePage()
        {
            NavigateTo("Console", ConsoleButton);
        }

        public void NavigateToDeployPage()
        {
            NavigateTo("Deploy", DeployButton);
        }

        private async Task UpdateStatusBarAsync()
        {
            try
            {
                var paths = new AppDataPaths();
                var serversService = new ServersJsonService(paths);
                var profiles = await serversService.LoadServersAsync();

                var runningCount = profiles.Count(p => p.Status == ServerStatus.Running);
                var totalCount = profiles.Count;

                string diskText;
                try
                {
                    var root = Path.GetPathRoot(paths.RootDirectory) ?? "C:\\";
                    var drive = new DriveInfo(root);
                    var freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    diskText = $" · {freeGb:0.0} GB free";
                }
                catch
                {
                    diskText = string.Empty;
                }

                var text = totalCount == 0
                    ? $"No servers configured{diskText}"
                    : $"{runningCount} of {totalCount} running{diskText}";

                await Dispatcher.InvokeAsync(() => StatusBarText.Text = text);
            }
            catch
            {
                // Status bar failures are silent
            }
        }

        public void OpenArkAsaSettings(ServerProfile profile)
        {
            PageTitleText.Text = "ARK ASA Settings";
            SetActiveButton(ServersButton);
            try
            {
                MainContentArea.Content = new ArkAsaSettingsView(profile);
            }
            catch (Exception ex)
            {
                GameServerManager.Services.AppDataPaths paths = new();
                var logPath = System.IO.Path.Combine(paths.LogsDirectory, "crash.log");
                System.IO.Directory.CreateDirectory(paths.LogsDirectory);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OpenArkAsaSettings: {ex}\n\n");
                System.Windows.MessageBox.Show($"Failed to open ARK ASA Settings:\n\n{ex.Message}\n\nSee Logs/crash.log for details.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void OpenGenericSettings(ServerProfile profile)
        {
            var registry = GameServerManager.GameProviders.GameProviderRegistry.CreateDefault();
            if (!registry.TryGetProvider(profile.GameId, out var provider) || !provider.SettingsDefinitions.Any())
            {
                System.Windows.MessageBox.Show(
                    $"No configurable settings are defined for {profile.GameId}.",
                    "Settings",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            PageTitleText.Text = $"{provider.GameName} Settings";
            SetActiveButton(ServersButton);
            MainContentArea.Content = new GenericServerSettingsView(profile, provider);
        }

        private void SetActiveButton(Button activeButton)
        {
            foreach (var button in new[]
                     {
                         DashboardButton,
                         ServersButton,
                         DeployButton,
                         FileManagerButton,
                         ConsoleButton,
                         BackupsButton,
                         SchedulerButton,
                         MonitoringButton,
                         UserManagementButton,
                         SettingsButton
                     })
            {
                button.Background = ReferenceEquals(button, activeButton) ? _activeBackground : _inactiveBackground;
                button.Foreground = ReferenceEquals(button, activeButton) ? _activeForeground : _inactiveForeground;
            }
        }

        private static UIElement CreatePlaceholderPage(string pageName)
        {
            var root = new Grid
            {
                Margin = new Thickness(18)
            };

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 22, 34, 52)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(72, 100, 125)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                MaxWidth = 760,
                MaxHeight = 360,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = pageName,
                Foreground = Brushes.White,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            stack.Children.Add(new TextBlock
            {
                Text = GetPlaceholderText(pageName),
                Foreground = new SolidColorBrush(Color.FromRgb(168, 183, 200)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            });

            card.Child = stack;
            root.Children.Add(card);
            return root;
        }

        private static string GetPlaceholderText(string pageName)
        {
            return pageName switch
            {
                "Servers" => "Server inventory, lifecycle controls, and profile status will live here.",
                "Deploy" => "Deployment flows for installing SteamCMD servers and creating profiles will live here.",
                "File Manager" => "Browse server installs, configs, saves, logs, and backup folders from this workspace.",
                "Console" => "RCON sessions and process output will be grouped here.",
                "Backups" => "Backup schedules, restore points, and retention status will live here.",
                "Scheduler" => "Restart windows, update jobs, and maintenance tasks will live here.",
                "Monitoring" => "Process metrics, ports, uptime, and alert history will live here.",
                "User Management" => "Operator accounts, roles, and access controls will live here.",
                "Settings" => "Application paths, SteamCMD setup, themes, and preferences will live here.",
                _ => "This section is ready for its workflow."
            };
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await new AppSettingsService().LoadAsync();
                ApplySettings(settings);
            }
            catch
            {
                ApplySettings(new AppSettings());
            }
        }

        private async Task CheckForUpdateBannerAsync()
        {
            try
            {
                var settings = await new AppSettingsService().LoadAsync();
                if (!settings.AutomaticallyCheckForUpdates)
                {
                    return;
                }

                var includeBeta = settings.IncludeBetaUpdates || string.Equals(settings.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
                var owner = string.IsNullOrWhiteSpace(settings.GitHubOwner) ? "joshcarterky" : settings.GitHubOwner.Trim();
                var repo = string.IsNullOrWhiteSpace(settings.GitHubRepository) ? "Gamer-server-manager" : settings.GitHubRepository.Trim();
                var result = await new GitHubReleaseService().CheckLatestAsync(
                    $"https://github.com/{owner}/{repo}",
                    AppVersion.Current,
                    includeBeta,
                    settings.SkippedUpdateVersion);

                if (!result.IsUpdateAvailable)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateBannerText.Text = $"Update available: {result.LatestVersion} ({result.UpdateType})";
                    UpdateBanner.Visibility = Visibility.Visible;
                });
            }
            catch
            {
                // Startup update notifications should never block the app shell.
            }
        }

        private static LinearGradientBrush CreateThemeBrush(string? theme)
        {
            var colors = (theme ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "darker" => ("#04080E", "#0B1320", "#120F1E", "#02040A"),
                "ocean" => ("#06131D", "#08344A", "#0B4A5D", "#041018"),
                "midnight" => ("#060A16", "#111B3A", "#1D1231", "#050611"),
                "forest" => ("#07120E", "#123322", "#163F2B", "#060B09"),
                "sunset" => ("#130D16", "#37203A", "#583044", "#0B0710"),
                "cyber blue" => ("#050B16", "#08284D", "#103B72", "#020713"),
                "neon purple" => ("#090712", "#21124A", "#35165E", "#06040D"),
                "high contrast" => ("#000000", "#0B1320", "#101827", "#000000"),
                "slate" => ("#0A1118", "#182536", "#20293A", "#070B10"),
                _ => ("#07111C", "#102C3D", "#20172E", "#070A12")
            };

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item1), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item2), 0.38),
                    new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item3), 0.78),
                    new GradientStop((Color)ColorConverter.ConvertFromString(colors.Item4), 1)
                }
            };
        }

        private static Brush CreateAccentBrush(string? accentColor)
        {
            var color = (accentColor ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "cyan" => "#1ECAFF",
                "purple" => "#8B5CF6",
                "green" => "#38D983",
                "orange" => "#FF9F1C",
                "red" => "#FF4D5E",
                _ => "#2F7BFF"
            };

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private void ApplyDensity(string? density)
        {
            var padding = (density ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "compact" => new Thickness(12, 7, 12, 7),
                "spacious" => new Thickness(16, 13, 16, 13),
                _ => new Thickness(14, 10, 14, 10)
            };

            foreach (var button in new[]
                     {
                         DashboardButton,
                         ServersButton,
                         DeployButton,
                         FileManagerButton,
                         ConsoleButton,
                         BackupsButton,
                         SchedulerButton,
                         MonitoringButton,
                         UserManagementButton,
                         SettingsButton
                     })
            {
                button.Padding = padding;
            }
        }
    }
}
