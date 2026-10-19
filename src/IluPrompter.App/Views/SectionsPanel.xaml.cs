using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.App.ViewModels;

namespace IluPrompter.App.Views;

public partial class SectionsPanel : UserControl
{
    public SectionsPanel() => InitializeComponent();

    /// <summary>The + icon or "Add section": insert a section marker at the caret.</summary>
    public event EventHandler? AddSectionRequested;

    public event EventHandler<SectionRowViewModel>? RenameRequested;

    public event EventHandler<SectionRowViewModel>? DeleteRequested;

    private SectionsViewModel? Model => DataContext as SectionsViewModel;

    private void OnAdd(object sender, RoutedEventArgs e) => AddSectionRequested?.Invoke(this, EventArgs.Empty);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        Model?.ShowInEditor(e.AddedItems[0] as SectionRowViewModel);
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: SectionRowViewModel row })
        {
            Model?.JumpTo(row);
            e.Handled = true;
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Model?.Selected is { } row)
        {
            Model.JumpTo(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Model is { IsFlyoutOpen: true } model)
        {
            model.IsFlyoutOpen = false;
            e.Handled = true;
        }
    }

    /// <summary>Right-click selects the row under the pointer, so the menu acts on it.</summary>
    private void OnListRightClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) is ListBoxItem item) item.IsSelected = true;
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Only over a row: an empty list or the gap below it has nothing to rename.
        if (ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) is not ListBoxItem) e.Handled = true;
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (Model?.Selected is { } row) RenameRequested?.Invoke(this, row);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Model?.Selected is { } row) DeleteRequested?.Invoke(this, row);
    }

    /// <summary>Move keyboard focus to the list (the flyout does this when it opens).</summary>
    public void FocusList()
    {
        List.Focus();
        if (List.SelectedItem is not null && List.ItemContainerGenerator.ContainerFromItem(List.SelectedItem) is ListBoxItem item) item.Focus();
    }
}
