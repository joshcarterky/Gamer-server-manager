using System.IO;
using System.Windows;
using System.Windows.Threading;
using GameServerManager.Services;

namespace GameServerManager.App
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
            MessageBox.Show($"An error occurred:\n\n{e.Exception.Message}\n\nSee crash.log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogCrash("UnhandledException", ex);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash("UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                var paths = new AppDataPaths();
                var logPath = Path.Combine(paths.LogsDirectory, "crash.log");
                Directory.CreateDirectory(paths.LogsDirectory);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n\n");
            }
            catch { }
        }
    }
}
