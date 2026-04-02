using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FactoryApp.Models;
using FactoryApp.Repositories;

namespace FactoryApp;

public partial class InventoryProductionDialog : Window
{
    private readonly InventoryRepository _inventoryRepository;

    public ObservableCollection<InventoryProductionEditRow> Rows { get; } = new();

    public InventoryProductionDialog(InventoryRepository inventoryRepository, IReadOnlyList<InventoryItem> snapshot)
    {
        InitializeComponent();
        _inventoryRepository = inventoryRepository;
        foreach (var item in snapshot)
        {
            Rows.Add(new InventoryProductionEditRow
            {
                Ddid = item.Ddid,
                Name = item.Name,
                CurrentProduced = item.Produced,
                AddAmountText = string.Empty
            });
        }

        RowsDataGrid.ItemsSource = Rows;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RowsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RowsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var updates = new List<(string Ddid, decimal NewTotal)>();
        foreach (var row in Rows)
        {
            var raw = (row.AddAmountText ?? string.Empty).Trim();
            if (raw.Length == 0)
                continue;

            if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var add) &&
                !decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out add))
            {
                MessageBox.Show(
                    $"قيمة «أضف» غير صحيحة.\nالصنف: {row.Name} ({row.Ddid})",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (add == 0m)
                continue;

            var newTotal = row.CurrentProduced + add;
            if (newTotal < 0m)
            {
                MessageBox.Show(
                    $"لا يمكن أن يصبح الإجمالي المُسجَّل سالباً.\nالصنف: {row.Name} ({row.Ddid})",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            updates.Add((row.Ddid, newTotal));
        }

        if (updates.Count == 0)
        {
            MessageBox.Show("لم تُدخل أي كمية في عمود «أضف».", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            foreach (var (ddid, newTotal) in updates)
                _inventoryRepository.SetProduced(ddid, newTotal);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر حفظ الإنتاج.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
