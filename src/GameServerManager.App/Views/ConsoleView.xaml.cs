using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameServerManager.App.ViewModels;
using GameServerManager.Core.Models;

namespace GameServerManager.App.Views;

public partial class ConsoleView : UserControl
{
    private ConsoleViewModel Vm => (ConsoleViewModel)DataContext;

    public ConsoleView(ServerProfile? initialProfile = null)
    {
        InitializeComponent();
        DataContext = new ConsoleViewModel(initialProfile);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Vm.FilteredLines.CollectionChanged += OnLinesChanged;
        CommandInput.KeyDown += OnCommandKeyDown;
        FilterBox.KeyDown += OnFilterKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Vm.FilteredLines.CollectionChanged -= OnLinesChanged;
        CommandInput.KeyDown -= OnCommandKeyDown;
        FilterBox.KeyDown -= OnFilterKeyDown;
        Vm.Dispose();
    }

    // ── Auto-scroll ───────────────────────────────────────────────────────────

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && Vm.AutoScroll)
        {
            // Scroll to bottom after layout pass
            Dispatcher.InvokeAsync(() =>
            {
                ConsoleScroller.ScrollToEnd();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    // ── Keyboard handling — command input ─────────────────────────────────────

    private void OnCommandKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift):
                if (Vm.SendCommand.CanExecute(null))
                    Vm.SendCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                Vm.HistoryUpCommand.Execute(null);
                // Move caret to end after history fills TextBox
                Dispatcher.InvokeAsync(() =>
                {
                    CommandInput.CaretIndex = CommandInput.Text.Length;
                }, System.Windows.Threading.DispatcherPriority.Input);
                e.Handled = true;
                break;

            case Key.Down:
                Vm.HistoryDownCommand.Execute(null);
                Dispatcher.InvokeAsync(() =>
                {
                    CommandInput.CaretIndex = CommandInput.Text.Length;
                }, System.Windows.Threading.DispatcherPriority.Input);
                e.Handled = true;
                break;

            case Key.Escape:
                Vm.CommandText = string.Empty;
                e.Handled = true;
                break;
        }
    }

    // ── Keyboard handling — filter box ────────────────────────────────────────

    private void OnFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Vm.FilterText = string.Empty;
            e.Handled = true;
        }
    }
}
