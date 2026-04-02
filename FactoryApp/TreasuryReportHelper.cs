using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FactoryApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors;
using WpfColor = System.Windows.Media.Color;

namespace FactoryApp;

/// <summary>طباعة وتصدير PDF لسجل الخزنة ورصيد أول المدة والملخص — جدول بحدود كاملة مثل الشاشة.</summary>
public static class TreasuryReportHelper
{
    private const float GridBorder = 0.75f;

    static TreasuryReportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePdf(
        decimal openingBalance,
        IReadOnlyList<TreasuryEntry> entries,
        decimal totalAdded,
        decimal totalTaken,
        string filePath)
    {
        var inv = CultureInfo.InvariantCulture;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(QColors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f));

                page.Header().Column(col =>
                {
                    col.Item().Text("الخزنة — سجل يومي").SemiBold().FontSize(16);
                    col.Item().PaddingTop(4).Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv)).FontSize(9).FontColor(QColors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"رصيد أول المدة: {openingBalance.ToString("N2", inv)}").SemiBold().FontSize(10);
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        // أوزان قريبة من أعمدة الـ DataGrid (التاريخ، رصيد اليوم السابق، …، السبب)
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(150);
                            c.RelativeColumn(125);
                            c.RelativeColumn(130);
                            c.RelativeColumn(115);
                            c.RelativeColumn(115);
                            c.RelativeColumn(220);
                        });

                        table.Header(h =>
                        {
                            PdfHeaderCell(h.Cell(), "التاريخ");
                            PdfHeaderCell(h.Cell(), "رصيد اليوم السابق", alignRight: true);
                            PdfHeaderCell(h.Cell(), "المبلغ الفعلي اليوم", alignRight: true);
                            PdfHeaderCell(h.Cell(), "المبلغ المضاف", alignRight: true);
                            PdfHeaderCell(h.Cell(), "المبلغ المسحوب", alignRight: true);
                            PdfHeaderCell(h.Cell(), "السبب");
                        });

                        foreach (var entry in entries)
                        {
                            var d = entry.Date?.ToString("yyyy-MM-dd", inv) ?? "—";
                            PdfBodyCell(table.Cell(), d);
                            PdfBodyCell(table.Cell(), entry.PreviousBalance.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.ActualAmountToday.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.AddedAmount.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.TakenAmount.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.Reason ?? string.Empty);
                        }
                    });

                    col.Item().PaddingTop(14).Text("ملخص الحركة").SemiBold().FontSize(12);
                    col.Item().PaddingTop(4).Text($"إجمالي المبلغ المضاف: {totalAdded.ToString("N2", inv)}");
                    col.Item().PaddingTop(2).Text($"إجمالي المبلغ المسحوب: {totalTaken.ToString("N2", inv)}");
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void PdfHeaderCell(IContainer cell, string text, bool alignRight = false)
    {
        var styled = cell
            .Border(GridBorder)
            .BorderColor(QColors.Grey.Darken2)
            .Background(QColors.Grey.Lighten3)
            .Padding(5);
        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).SemiBold().FontSize(9);
        else
            styled.AlignMiddle().Text(text).SemiBold().FontSize(9);
    }

    private static void PdfBodyCell(IContainer cell, string text, bool alignRight = false)
    {
        var styled = cell
            .Border(GridBorder)
            .BorderColor(QColors.Grey.Darken2)
            .Background(QColors.White)
            .Padding(4);
        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).FontSize(9);
        else
            styled.AlignMiddle().Text(text).FontSize(9);
    }

    public static void Print(
        decimal openingBalance,
        IReadOnlyList<TreasuryEntry> entries,
        decimal totalAdded,
        decimal totalTaken)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return;

        var doc = BuildFlowDocument(openingBalance, entries, totalAdded, totalTaken);
        doc.ColumnWidth = printDialog.PrintableAreaWidth;
        printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "الخزنة");
    }

    private static FlowDocument BuildFlowDocument(
        decimal openingBalance,
        IReadOnlyList<TreasuryEntry> entries,
        decimal totalAdded,
        decimal totalTaken)
    {
        var inv = CultureInfo.InvariantCulture;
        var gridBrush = new SolidColorBrush(WpfColor.FromRgb(90, 90, 90));
        var headerBg = new SolidColorBrush(WpfColor.FromRgb(235, 235, 238));
        var cellBorder = new Thickness(0.75);
        var cellPad = new Thickness(6, 5, 6, 5);

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(48),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FlowDirection = FlowDirection.RightToLeft
        };

        doc.Blocks.Add(new Paragraph(new Run("الخزنة — سجل يومي"))
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        doc.Blocks.Add(new Paragraph(new Run(DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv)))
        {
            Foreground = new SolidColorBrush(WpfColor.FromRgb(100, 100, 100)),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 12)
        });

        doc.Blocks.Add(new Paragraph(new Run($"رصيد أول المدة: {openingBalance.ToString("N2", inv)}"))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var table = new Table { CellSpacing = 0 };
        foreach (var w in new[]
                 {
                     new GridLength(118),
                     new GridLength(102),
                     new GridLength(108),
                     new GridLength(96),
                     new GridLength(96),
                     new GridLength(1, GridUnitType.Star)
                 })
            table.Columns.Add(new TableColumn { Width = w });

        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow();
        foreach (var h in new[] { "التاريخ", "رصيد اليوم السابق", "المبلغ الفعلي اليوم", "المبلغ المضاف", "المبلغ المسحوب", "السبب" })
        {
            var p = new Paragraph(new Run(h))
            {
                FontWeight = FontWeights.SemiBold,
                Margin = cellPad,
                FontSize = 10
            };
            headerRow.Cells.Add(new TableCell(p)
            {
                BorderBrush = gridBrush,
                BorderThickness = cellBorder,
                Background = headerBg
            });
        }
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        foreach (var entry in entries)
        {
            var row = new TableRow();
            var d = entry.Date?.ToString("yyyy-MM-dd", inv) ?? "—";
            row.Cells.Add(PrintDataCell(d, gridBrush, cellBorder, cellPad, right: false));
            row.Cells.Add(PrintDataCell(entry.PreviousBalance.ToString("N2", inv), gridBrush, cellBorder, cellPad, right: true));
            row.Cells.Add(PrintDataCell(entry.ActualAmountToday.ToString("N2", inv), gridBrush, cellBorder, cellPad, right: true));
            row.Cells.Add(PrintDataCell(entry.AddedAmount.ToString("N2", inv), gridBrush, cellBorder, cellPad, right: true));
            row.Cells.Add(PrintDataCell(entry.TakenAmount.ToString("N2", inv), gridBrush, cellBorder, cellPad, right: true));
            row.Cells.Add(PrintDataCell(entry.Reason ?? string.Empty, gridBrush, cellBorder, cellPad, right: false));
            bodyGroup.Rows.Add(row);
        }
        table.RowGroups.Add(bodyGroup);

        table.Margin = new Thickness(0, 0, 0, 16);
        doc.Blocks.Add(table);

        doc.Blocks.Add(new Paragraph(new Run("ملخص الحركة")) { FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 8, 0, 6) });
        doc.Blocks.Add(new Paragraph(new Run($"إجمالي المبلغ المضاف: {totalAdded.ToString("N2", inv)}")));
        doc.Blocks.Add(new Paragraph(new Run($"إجمالي المبلغ المسحوب: {totalTaken.ToString("N2", inv)}")));

        return doc;
    }

    private static TableCell PrintDataCell(string text, Brush borderBrush, Thickness border, Thickness pad, bool right)
    {
        var p = new Paragraph(new Run(text)) { Margin = pad, FontSize = 10 };
        if (right)
            p.TextAlignment = TextAlignment.Right;
        return new TableCell(p)
        {
            BorderBrush = borderBrush,
            BorderThickness = border,
            Background = Brushes.White
        };
    }
}
