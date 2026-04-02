using System.Globalization;
using System.Linq;
using FactoryApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors;

namespace FactoryApp;

/// <summary>تصدير جدول المخزن إلى PDF بحدود كاملة (نفس أسلوب المصنع والمورد).</summary>
public static class InventoryReportHelper
{
    private const float GridBorder = 0.75f;

    static InventoryReportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePdf(IReadOnlyList<InventoryItem> items, string filePath)
    {
        var inv = CultureInfo.InvariantCulture;
        var sumOrdered = items.Sum(x => x.TotalOrderedOnCustomerReceipts);
        var sumDelivered = items.Sum(x => x.TotalDeliveredToCustomers);
        var sumNeed = items.Sum(x => x.CurrentOutstandingCustomerNeed);
        var sumAvailable = items.Sum(x => x.AvailableInStorage);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(QColors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f));

                page.Header().Column(h =>
                {
                    h.Item().Text("المخزن — مركز التحكم بالمخزون").SemiBold().FontSize(16);
                    h.Item().PaddingTop(4).Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", inv))
                        .FontSize(9).FontColor(QColors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(88);
                            c.RelativeColumn(160);
                            c.RelativeColumn(72);
                            c.RelativeColumn(72);
                            c.RelativeColumn(72);
                            c.RelativeColumn(80);
                        });

                        table.Header(header =>
                        {
                            PdfHeaderCell(header.Cell(), "DDID");
                            PdfHeaderCell(header.Cell(), "الاسم");
                            PdfHeaderCell(header.Cell(), "طلبات العملاء", alignRight: true);
                            PdfHeaderCell(header.Cell(), "مسلّم للعملاء", alignRight: true);
                            PdfHeaderCell(header.Cell(), "الاحتياج", alignRight: true);
                            PdfHeaderCell(header.Cell(), "المتوفر في المخزن", alignRight: true);
                        });

                        foreach (var item in items)
                        {
                            PdfBodyCell(table.Cell(), string.IsNullOrWhiteSpace(item.Ddid) ? "—" : item.Ddid);
                            PdfBodyCell(table.Cell(), string.IsNullOrWhiteSpace(item.Name) ? "—" : item.Name);
                            PdfBodyCell(table.Cell(), item.TotalOrderedOnCustomerReceipts.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), item.TotalDeliveredToCustomers.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), item.CurrentOutstandingCustomerNeed.ToString("N2", inv), alignRight: true);
                            PdfBodyCell(table.Cell(), item.AvailableInStorage.ToString("N2", inv), alignRight: true);
                        }

                        PdfTotalCell(table.Cell(), "المجموع");
                        PdfTotalCell(table.Cell(), "");
                        PdfTotalCell(table.Cell(), sumOrdered.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), sumDelivered.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), sumNeed.ToString("N2", inv), alignRight: true);
                        PdfTotalCell(table.Cell(), sumAvailable.ToString("N2", inv), alignRight: true);
                    });

                    col.Item().PaddingTop(10).Text("ملاحظة: عمود الصورة المصغّرة غير مضمّن في التقرير؛ الأرقام مطابقة لجدول الشاشة.")
                        .FontSize(8).FontColor(QColors.Grey.Darken1);
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
            .Padding(5);
        if (string.IsNullOrEmpty(text))
        {
            styled.MinHeight(10);
            return;
        }

        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).SemiBold().FontSize(9);
        else
            styled.AlignMiddle().Text(text).SemiBold().FontSize(9);
    }
}
