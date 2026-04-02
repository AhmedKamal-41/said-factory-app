using System.Globalization;
using System.Linq;
using FactoryApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FactoryApp;

/// <summary>QuestPDF export for customer receipts, delivery sections, payments, and summary — جداول بحدود كاملة.</summary>
public static class CustomerReportHelper
{
    private const float GridBorder = 0.75f;

    static CustomerReportHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePdf(Customer customer, CustomerExportScope scope, bool showDiscount, string filePath)
    {
        var inv = CultureInfo.InvariantCulture;
        string FDate(DateTime d) => d.ToString("dd/MM/yyyy", inv);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(h =>
                {
                    h.Item().Text($"حساب العميل — {customer.Name}").SemiBold().FontSize(16);
                    h.Item().PaddingTop(4).Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", inv))
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(column =>
                {
                    switch (scope)
                    {
                        case CustomerExportScope.Receipts:
                            ReceiptsBlock(column, customer, showDiscount, FDate, inv);
                            break;
                        case CustomerExportScope.DeliverySections:
                            SectionsBlock(column, customer, FDate, inv);
                            break;
                        case CustomerExportScope.Payments:
                            PaymentsBlock(column, customer, FDate, inv);
                            break;
                        case CustomerExportScope.FullPage:
                            ReceiptsBlock(column, customer, showDiscount, FDate, inv);
                            column.Item().Height(10);
                            SectionsBlock(column, customer, FDate, inv);
                            column.Item().Height(10);
                            PaymentsBlock(column, customer, FDate, inv);
                            column.Item().Height(10);
                            SummaryBlock(column, customer, inv);
                            break;
                    }
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
            .BorderColor(Colors.Grey.Darken2)
            .Background(Colors.White)
            .Padding(4);
        if (alignRight)
            styled.AlignRight().AlignMiddle().Text(text).FontSize(9);
        else
            styled.AlignMiddle().Text(text).FontSize(9);
    }

    private static void ReceiptsBlock(ColumnDescriptor column, Customer customer, bool showDiscount,
        Func<DateTime, string> fDate, CultureInfo inv)
    {
        column.Item().PaddingBottom(6).Text("إيصالات المبيعات").SemiBold().FontSize(12);
        column.Item().Table(table =>
        {
            if (showDiscount)
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(72);
                    c.RelativeColumn(52);
                    c.RelativeColumn(140);
                    c.RelativeColumn(48);
                    c.RelativeColumn(48);
                    c.RelativeColumn(40);
                    c.RelativeColumn(52);
                    c.RelativeColumn(48);
                    c.RelativeColumn(56);
                });
                table.Header(header =>
                {
                    PdfHeaderCell(header.Cell(), "التاريخ");
                    PdfHeaderCell(header.Cell(), "DDID");
                    PdfHeaderCell(header.Cell(), "الصنف");
                    PdfHeaderCell(header.Cell(), "الكمية", alignRight: true);
                    PdfHeaderCell(header.Cell(), "سعر القطعة", alignRight: true);
                    PdfHeaderCell(header.Cell(), "خصم", alignRight: true);
                    PdfHeaderCell(header.Cell(), "الإجمالي", alignRight: true);
                    PdfHeaderCell(header.Cell(), "العربون", alignRight: true);
                    PdfHeaderCell(header.Cell(), "المتبقي", alignRight: true);
                });
                foreach (var r in customer.Receipts)
                {
                    PdfBodyCell(table.Cell(), fDate(r.Date));
                    PdfBodyCell(table.Cell(), r.Ddid ?? "—");
                    PdfBodyCell(table.Cell(), r.Kind ?? "");
                    PdfBodyCell(table.Cell(), r.Quantity.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.PricePerPiece.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Discount.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Total.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Deposit.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Remaining.ToString("N2", inv), alignRight: true);
                }
            }
            else
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(72);
                    c.RelativeColumn(52);
                    c.RelativeColumn(160);
                    c.RelativeColumn(52);
                    c.RelativeColumn(52);
                    c.RelativeColumn(56);
                    c.RelativeColumn(52);
                    c.RelativeColumn(56);
                });
                table.Header(header =>
                {
                    PdfHeaderCell(header.Cell(), "التاريخ");
                    PdfHeaderCell(header.Cell(), "DDID");
                    PdfHeaderCell(header.Cell(), "الصنف");
                    PdfHeaderCell(header.Cell(), "الكمية", alignRight: true);
                    PdfHeaderCell(header.Cell(), "سعر القطعة", alignRight: true);
                    PdfHeaderCell(header.Cell(), "الإجمالي", alignRight: true);
                    PdfHeaderCell(header.Cell(), "العربون", alignRight: true);
                    PdfHeaderCell(header.Cell(), "المتبقي", alignRight: true);
                });
                foreach (var r in customer.Receipts)
                {
                    PdfBodyCell(table.Cell(), fDate(r.Date));
                    PdfBodyCell(table.Cell(), r.Ddid ?? "—");
                    PdfBodyCell(table.Cell(), r.Kind ?? "");
                    PdfBodyCell(table.Cell(), r.Quantity.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.PricePerPiece.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Total.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Deposit.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), r.Remaining.ToString("N2", inv), alignRight: true);
                }
            }
        });
    }

    private static void SectionsBlock(ColumnDescriptor column, Customer customer, Func<DateTime, string> fDate, CultureInfo inv)
    {
        column.Item().PaddingBottom(6).Text("أقسام التسليم").SemiBold().FontSize(12);
        var receipts = customer.Receipts.OrderBy(x => x.ReceiptId).ToList();
        var any = receipts.Sum(x => x.Sections.Count) > 0;

        if (!any)
        {
            column.Item().Text("لا توجد أقسام تسليم.").FontColor(Colors.Grey.Medium).FontSize(10);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(64);
                c.RelativeColumn(160);
                c.RelativeColumn(88);
                c.RelativeColumn(56);
                c.RelativeColumn(72);
            });
            table.Header(header =>
            {
                PdfHeaderCell(header.Cell(), "DDID");
                PdfHeaderCell(header.Cell(), "الصنف");
                PdfHeaderCell(header.Cell(), "تاريخ التسليم");
                PdfHeaderCell(header.Cell(), "مسلّم", alignRight: true);
                PdfHeaderCell(header.Cell(), "متبقي الإيصال", alignRight: true);
            });
            foreach (var r in receipts)
            {
                var deliveredOnReceipt = r.Sections.Sum(s => s.Quantity);
                var remainingOnReceipt = r.Quantity - deliveredOnReceipt;
                foreach (var s in r.Sections)
                {
                    PdfBodyCell(table.Cell(), string.IsNullOrWhiteSpace(r.Ddid) ? "—" : r.Ddid);
                    PdfBodyCell(table.Cell(), string.IsNullOrWhiteSpace(r.Kind) ? "—" : r.Kind);
                    PdfBodyCell(table.Cell(), fDate(s.DeliveryDate));
                    PdfBodyCell(table.Cell(), s.Quantity.ToString("N2", inv), alignRight: true);
                    PdfBodyCell(table.Cell(), remainingOnReceipt.ToString("N2", inv), alignRight: true);
                }
            }
        });
    }

    private static void PaymentsBlock(ColumnDescriptor column, Customer customer, Func<DateTime, string> fDate, CultureInfo inv)
    {
        column.Item().PaddingBottom(6).Text("جدول الدفعات").SemiBold().FontSize(12);
        var rows = BuildPaymentRows(customer);

        if (rows.Count == 0)
        {
            column.Item().Text("لا توجد دفعات.").FontColor(Colors.Grey.Medium).FontSize(10);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(88);
                c.RelativeColumn(48);
                c.RelativeColumn(56);
                c.RelativeColumn(72);
                c.RelativeColumn(200);
            });
            table.Header(header =>
            {
                PdfHeaderCell(header.Cell(), "تاريخ الاستحقاق");
                PdfHeaderCell(header.Cell(), "تم السداد");
                PdfHeaderCell(header.Cell(), "المبلغ", alignRight: true);
                PdfHeaderCell(header.Cell(), "المتبقي بعد الدفع", alignRight: true);
                PdfHeaderCell(header.Cell(), "ملاحظة");
            });
            foreach (var x in rows)
            {
                PdfBodyCell(table.Cell(), fDate(x.Payment.PaymentDate));
                PdfBodyCell(table.Cell(), x.Payment.IsPaid ? "نعم" : "لا");
                PdfBodyCell(table.Cell(), x.Payment.Amount.ToString("N2", inv), alignRight: true);
                PdfBodyCell(table.Cell(), x.RemainingAfter.ToString("N2", inv), alignRight: true);
                PdfBodyCell(table.Cell(), x.Payment.Note ?? "");
            }
        });
    }

    private static List<(CustomerPayment Payment, decimal RemainingAfter)> BuildPaymentRows(Customer customer)
    {
        decimal running = customer.Receipts.Sum(re => re.Total - re.Deposit);
        var ordered = customer.Receipts
            .SelectMany(re => re.Payments.Select(p => (Receipt: re, Payment: p)))
            .OrderBy(x => x.Payment.PaymentDate)
            .ThenBy(x => x.Receipt.ReceiptId)
            .ThenBy(x => x.Payment.PaymentId)
            .ToList();
        var list = new List<(CustomerPayment, decimal)>();
        foreach (var x in ordered)
        {
            if (x.Payment.IsPaid)
                running -= x.Payment.Amount;
            list.Add((x.Payment, running));
        }
        return list;
    }

    private static void SummaryBlock(ColumnDescriptor column, Customer customer, CultureInfo inv)
    {
        decimal totalQty = 0, totalSales = 0, totalPaid = 0;
        foreach (var r in customer.Receipts)
        {
            totalQty += r.Quantity;
            totalSales += r.Total;
            totalPaid += r.Deposit;
            foreach (var p in r.Payments)
                if (p.IsPaid)
                    totalPaid += p.Amount;
        }
        var totalRemaining = totalSales - totalPaid;

        column.Item().PaddingBottom(6).Text("ملخص الحساب").SemiBold().FontSize(12);
        column.Item().Border(GridBorder).BorderColor(Colors.Grey.Darken2).Background(Colors.Grey.Lighten4).Padding(12).Column(s =>
        {
            s.Item().Text($"إجمالي عدد القطع: {totalQty.ToString("N2", inv)}");
            s.Item().PaddingTop(4).Text($"إجمالي المبيعات: {totalSales.ToString("N2", inv)}");
            s.Item().PaddingTop(4).Text($"إجمالي المدفوع: {totalPaid.ToString("N2", inv)}");
            s.Item().PaddingTop(4).Text($"إجمالي المتبقي: {totalRemaining.ToString("N2", inv)}").SemiBold();
        });
    }
}
