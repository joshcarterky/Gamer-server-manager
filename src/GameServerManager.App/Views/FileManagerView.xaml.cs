using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GameServerManager.App.ViewModels;
using GameServerManager.Core.Models;

namespace GameServerManager.App.Views;

public partial class FileManagerView : UserControl
{
    private FileManagerViewModel ViewModel => (FileManagerViewModel)DataContext;

    public FileManagerView(ServerProfile? initialProfile = null)
    {
        InitializeComponent();
        DataContext = new FileManagerViewModel(initialProfile);
    }

    // ── Tree view ─────────────────────────────────────────────────────────────

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderTreeNode node)
            _ = ViewModel.NavigateToAsync(node.FullPath);
    }

    private async void OnTreeItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is FolderTreeNode node)
            await ViewModel.ExpandTreeNodeAsync(node);
    }

    // ── File list ─────────────────────────────────────────────────────────────

    private void OnFileListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = FileList.SelectedItems.Cast<FileEntryViewModel>();
        ViewModel.SetSelectedEntries(selected);
    }

    private void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is FileEntryViewModel entry)
            _ = ViewModel.OpenEntryAsync(entry);
    }

    private void OnFileListKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (FileList.SelectedItem is FileEntryViewModel entry)
                    _ = ViewModel.OpenEntryAsync(entry);
                e.Handled = true;
                break;

            case Key.Delete:
                ViewModel.DeleteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F2:
                ViewModel.RenameCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Back:
                ViewModel.NavigateUpCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnFileListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.CopyCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.CutCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.PasteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FileList.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.SaveFileCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = ViewModel.CommitRenameAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameBoxFocused(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            // Select name without extension for files
            var name = tb.Text;
            var dotIdx = name.LastIndexOf('.');
            if (dotIdx > 0)
            {
                tb.Select(0, dotIdx);
            }
            else
            {
                tb.SelectAll();
            }
        }
    }
}
