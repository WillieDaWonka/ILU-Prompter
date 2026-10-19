using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.App.Logic;

namespace IluPrompter.App.Views.Pages;

public partial class ShortcutsPage : UserControl
{
    public ShortcutsPage() => InitializeComponent();

    /// <summary>
    /// "Print this list". The print dialog is the Windows one (like Open and Save, it follows the Windows theme);
    /// the page itself is black on white paper.
    /// </summary>
    private void OnPrint(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        var document = BuildDocument(dialog.PrintableAreaWidth);
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "ILU Prompter shortcuts");
    }

    public static FlowDocument BuildDocument(double pageWidth)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(48),
            PageWidth = pageWidth > 0 ? pageWidth : 794,
            ColumnWidth = 10000,
        };
        document.Blocks.Add(new Paragraph(new Run("ILU Prompter: keyboard shortcuts")) { FontSize = 20, FontWeight = FontWeights.SemiBold });
        foreach (var group in ShortcutCatalog.Groups)
        {
            document.Blocks.Add(new Paragraph(new Run(group.Title)) { FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 0) });
            document.Blocks.Add(new Paragraph(new Run(group.Caption)) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 4) });
            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(220) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var rows = new TableRowGroup();
            foreach (var entry in group.Entries)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Keys)) { FontFamily = new FontFamily("Consolas") }));
                var action = new Paragraph(new Run(entry.Action));
                if (entry.Note is { Length: > 0 }) action.Inlines.Add(new Run(" (" + entry.Note + ")") { Foreground = Brushes.DimGray });
                row.Cells.Add(new TableCell(action));
                rows.Rows.Add(row);
            }

            table.RowGroups.Add(rows);
            document.Blocks.Add(table);
        }

        document.Blocks.Add(new Paragraph(new Run(ShortcutCatalog.ClickerNote)) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 16, 0, 0) });
        return document;
    }
}
