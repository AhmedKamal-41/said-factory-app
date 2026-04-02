using System.Globalization;
using System.Linq;
using FactoryApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactoryApp;

/// <summary>تصدير حسابات المصنع إلى PDF بجدول بحدود كاملة (نفس أسلوب الخزنة والعملاء).</summary>
public static class FactoryReportHelper
{
    private const float GridBorder = 0.75f;

    static FactoryReportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePdf(IReadOnlyList<FactoryEntry> entries, string filePath)
    {
        var inv = CultureInfo.InvariantCulture;
        var totalPrice = entries.Sum(x => x.TotalPrice);
        var totalShirts = entries.Sum(x => x.ShirtCount);
        var totalMfg = entries.Sum(x => x.ManufacturingCost);
        var totalWaste = entries.Sum(x => x.Waste);
        var totalPrinting = entries.Sum(x => x.PrintingCost);
        var totalCostPerShirt = entries.Sum(x => x.CostPerShirt);
        var totalCostAll = entries.Sum(x => x.TotalCostAll);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(h =>
                {
                    h.Item().Text("حسابات المصنع — جدول الحسابات").SemiBold().FontSize(16);
                    h.Item().PaddingTop(4).Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", inv))
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(150);
                            c.RelativeColumn(56);
                            c.RelativeColumn(64);
                            c.RelativeColumn(72);
                            c.RelativeColumn(72);
                            c.RelativeColumn(72);
                            c.RelativeColumn(64);
                            c.RelativeColumn(56);
                            c.RelativeColumn(56);
                            c.RelativeColumn(80);
                            c.RelativeColumn(72);
                        });

                        table.Header(header =>
                        {
                            PdfHeaderCell(header.Cell(), "الاسم");
                            PdfHeaderCell(header.Cell(), "كيلو", alignRight: true);
                            PdfHeaderCell(header.Cell(), "سعر الكيلو", alignRight: true);
                            PdfHeaderCell(header.Cell(), "إجمالي السعر", alignRight: true);
                            PdfHeaderCell(header.Cell(), "عدد التي شيرت", alignRight: true);
                            PdfHeaderCell(header.Cell(), "سعر القطعة", alignRight: true);
                            PdfHeaderCell(header.Cell(), "المصنعية", alignRight: true);
                            PdfHeaderCell(header.Cell(), "الهالك", alignRight: true);
                            PdfHeaderCell(header.Cell(), "طباعة", alignRight: true);
                            PdfHeaderCell(header.Cell(), "تكلفة التي شيرت", alignRight: true);
                            PdfHeaderCell(header.Cell(), "التكلفة الكل", alignRight: true);
                        });

                        foreach (var e in entries)
                        {
                            PdfBodyCell(table.Cell(), string.IsNullOrWhiteSpace(e.Name) ? "—" : e.Name);
                            PdfBodyCell(table.Cell(), e.Kilo.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.PricePerKilo.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.TotalPrice.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.ShirtCount.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.PricePerShirt.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.ManufacturingCost.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.Waste.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.PrintingCost.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.CostPerShirt.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), e.TotalCostAll.ToString("N2", inv), alignRight: true);
                        }
                    });

                    col.Item().PaddingTop(14).Text("ملخص المجاميع").SemiBold().FontSize(12);
                    col.Item().Border(GridBorder).BorderColor(Colors.Grey.Darken2).Background(Colors.Grey.Lighten4).Padding(12).Column(s =>
                    {
                        s.Item().Text($"إجمالي السعر: {totalPrice.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"عدد التي شيرت: {totalShirts.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"المصنعية: {totalMfg.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"الهالك: {totalWaste.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"طباعة: {totalPrinting.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"تكلفة التي شيرت (مجموع): {totalCostPerShirt.ToString("N2", inv)}");
                        s.Item().PaddingTop(4).Text($"التكلفة الكل: {totalCostAll.ToString("N2", inv)}").SemiBold();
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void PdfHeaderCell(IContainer cell, string text, bool alignRight = false)
    {
        var styled = cell
            .Border(GridBorder)
            .BorderColor(Colors.Grey.Darken2)
            .Background(Colors.Grey.Lighten3)
            .Padding(4);
        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).SemiBold().FontSize(8);
        else
            styled.AlignMiddle().Text(text).SemiBold().FontSize(8);
    }

    private static void PdfBodyCell(IContainer cell, string text, bool alignRight = false)
    {
        var styled = cell
            .Border(GridBorder)
            .BorderColor(Colors.Grey.Darken2)
            .Background(Colors.White)
            .Padding(3);
        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).FontSize(8);
        else
            styled.AlignMiddle().Text(text).FontSize(8);
    }
}
