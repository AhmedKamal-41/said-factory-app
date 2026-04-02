using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FactoryApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors;
using WpfColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;

namespace FactoryApp;

/// <summary>طباعة وتصدير PDF للمورد — جدول بحدود كاملة كالخزنة والعملاء.</summary>
public static class SupplierReportHelper
{
    private const float GridBorder = 0.75f;

    static SupplierReportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePdf(Supplier supplier, string filePath)
    {
        var inv = CultureInfo.InvariantCulture;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(QColors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("تقرير المورد — " + supplier.Name).SemiBold().FontSize(16);
                    column.Item().PaddingTop(4).Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv)).FontSize(9).FontColor(QColors.Grey.Darken2);
                });

                page.Content().Column(column =>
                {
                    var receiptPaths = supplier.ReceiptImagePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).ToList();
                    if (receiptPaths.Count > 0)
                    {
                        column.Item().PaddingBottom(8).Text("صور الإيصال:").SemiBold().FontSize(11);
                        foreach (var imgPath in receiptPaths)
                        {
                            try
                            {
                                column.Item().PaddingBottom(12).MaxHeight(200).Image(imgPath).FitArea();
                            }
                            catch
                            {
                                column.Item().Text("(تعذر تحميل صورة)").FontColor(QColors.Grey.Medium);
                            }
                        }
                    }

                    column.Item().PaddingBottom(6).Text("سجل الأعمال").SemiBold().FontSize(12);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(108);
                            columns.RelativeColumn(140);
                            columns.RelativeColumn(56);
                            columns.RelativeColumn(56);
                            columns.RelativeColumn(56);
                            columns.RelativeColumn(56);
                            columns.RelativeColumn(56);
                        });

                        table.Header(header =>
                        {
                            PdfHeaderCell(header.Cell(), "وقت الشراء");
                            PdfHeaderCell(header.Cell(), "الخامة");
                            PdfHeaderCell(header.Cell(), "الكمية kg", alignRight: true);
                            PdfHeaderCell(header.Cell(), "سعر الكيلو", alignRight: true);
                            PdfHeaderCell(header.Cell(), "Total", alignRight: true);
                            PdfHeaderCell(header.Cell(), "المدفوع", alignRight: true);
                            PdfHeaderCell(header.Cell(), "الباقي", alignRight: true);
                        });

                        foreach (var entry in supplier.Entries)
                        {
                            PdfBodyCell(table.Cell(), entry.PurchaseDate.ToString("yyyy-MM-dd HH:mm", inv));
                            PdfBodyCell(table.Cell(), entry.MaterialName ?? "");
                            PdfBodyCell(table.Cell(), entry.QuantityKg.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.PricePerKg.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.TotalPrice.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.Paid.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), entry.Remaining.ToString("N2", inv), alignRight: true);
                        }

                        var totalPrice = supplier.Entries.Sum(e => e.TotalPrice);
                        var totalPaid = supplier.Entries.Sum(e => e.Paid);
                        var totalRemaining = supplier.Entries.Sum(e => e.Remaining);
                        var totalQty = supplier.Entries.Sum(e => e.QuantityKg);

                        PdfTotalCell(table.Cell(), "المجموع");
                        PdfTotalCell(table.Cell(), "");
                        PdfTotalCell(table.Cell(), totalQty.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), "");
                        PdfTotalCell(table.Cell(), totalPrice.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), totalPaid.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), totalRemaining.ToString("N2", inv), alignRight: true);
                    });
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

    private static void PdfTotalCell(IContainer cell, string text, bool alignRight = false)
    {
        var styled = cell
            .Border(GridBorder)
            .BorderColor(QColors.Grey.Darken2)
            .Background(QColors.Grey.Lighten4)
            .Padding(6);
        if (string.IsNullOrEmpty(text))
        {
            styled.MinHeight(12);
            return;
        }

        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).SemiBold().FontSize(9);
        else
            styled.AlignMiddle().Text(text).SemiBold().FontSize(9);
    }

    public static void Print(Supplier supplier)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return;

        var printContent = BuildPrintableVisual(supplier);
        printContent.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, double.PositiveInfinity));
        printContent.Arrange(new Rect(0, 0, printDialog.PrintableAreaWidth, printContent.DesiredSize.Height));
        printDialog.PrintVisual(printContent, "تقرير المورد — " + supplier.Name);
    }

    private static FrameworkElement BuildPrintableVisual(Supplier supplier)
    {
        var gridBrush = new SolidColorBrush(WpfColor.FromRgb(90, 90, 90));
        var headerBg = new SolidColorBrush(WpfColor.FromRgb(235, 235, 238));
        var totalBg = new SolidColorBrush(WpfColor.FromRgb(245, 245, 248));
        var border = new Thickness(0.75);
        var pad = new Thickness(6, 5, 6, 5);

        var stack = new StackPanel { Margin = new Thickness(24) };

        stack.Children.Add(new TextBlock
        {
            Text = "تقرير المورد — " + supplier.Name,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            FontSize = 11,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(100, 100, 100)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var receiptPaths = supplier.ReceiptImagePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).ToList();
        if (receiptPaths.Count > 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "صور الإيصال",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            foreach (var path in receiptPaths)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(Path.GetFullPath(path));
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    stack.Children.Add(new WpfImage
                    {
                        Source = bitmap,
                        MaxWidth = 300,
                        MaxHeight = 200,
                        Margin = new Thickness(0, 0, 0, 12)
                    });
                }
                catch { /* ignore */ }
            }
        }

        stack.Children.Add(new TextBlock
        {
            Text = "سجل الأعمال",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var grid = new Grid();
        foreach (var w in new[]
                 {
                     new GridLength(112),
                     new GridLength(1, GridUnitType.Star),
                     new GridLength(80),
                     new GridLength(80),
                     new GridLength(80),
                     new GridLength(80),
                     new GridLength(80)
                 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });

        var rowCount = 2 + supplier.Entries.Count;
        for (var i = 0; i < rowCount; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        static Border PrintCell(string text, bool header, bool totalRow, TextAlignment align, Brush gridLine, Thickness b, Thickness p, Brush headerBrush, Brush totalBrush)
        {
            var tb = new TextBlock
            {
                Text = text,
                Padding = p,
                FontSize = 10,
                TextAlignment = align
            };
            if (header || totalRow)
                tb.FontWeight = FontWeights.SemiBold;
            return new Border
            {
                BorderBrush = gridLine,
                BorderThickness = b,
                Background = header ? headerBrush : totalRow ? totalBrush : Brushes.White,
                Child = tb
            };
        }

        var headers = new[] { "وقت الشراء", "الخامة", "الكمية kg", "سعر الكيلو", "Total", "المدفوع", "الباقي" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = PrintCell(headers[c], true, false, TextAlignment.Right, gridBrush, border, pad, headerBg, totalBg);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        var row = 1;
        foreach (var entry in supplier.Entries)
        {
            var cells = new[]
            {
                (entry.PurchaseDate.ToString("yyyy-MM-dd HH:mm"), TextAlignment.Right),
                (entry.MaterialName ?? "", TextAlignment.Right),
                (entry.QuantityKg.ToString("N2"), TextAlignment.Right),
                (entry.PricePerKg.ToString("N2"), TextAlignment.Right),
                (entry.TotalPrice.ToString("N2"), TextAlignment.Right),
                (entry.Paid.ToString("N2"), TextAlignment.Right),
                (entry.Remaining.ToString("N2"), TextAlignment.Right)
            };
            for (var c = 0; c < cells.Length; c++)
            {
                var cell = PrintCell(cells[c].Item1, false, false, cells[c].Item2, gridBrush, border, pad, headerBg, totalBg);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
            row++;
        }

        var totalPrice = supplier.Entries.Sum(e => e.TotalPrice);
        var totalPaid = supplier.Entries.Sum(e => e.Paid);
        var totalRemaining = supplier.Entries.Sum(e => e.Remaining);
        var totalQty = supplier.Entries.Sum(e => e.QuantityKg);
        var totals = new[]
        {
            ("المجموع", TextAlignment.Right),
            ("", TextAlignment.Right),
            (totalQty.ToString("N2"), TextAlignment.Right),
            ("", TextAlignment.Right),
            (totalPrice.ToString("N2"), TextAlignment.Right),
            (totalPaid.ToString("N2"), TextAlignment.Right),
            (totalRemaining.ToString("N2"), TextAlignment.Right)
        };
        for (var c = 0; c < totals.Length; c++)
        {
            var cell = PrintCell(totals[c].Item1, false, true, totals[c].Item2, gridBrush, border, pad, headerBg, totalBg);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        stack.Children.Add(grid);
        return new Border { Child = stack, Background = Brushes.White };
    }
}
