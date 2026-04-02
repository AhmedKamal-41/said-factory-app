using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FactoryApp.Models;
using FactoryApp.Repositories;
using FactoryApp.Services;
using Microsoft.Win32;

namespace FactoryApp;

public partial class SupplierWindow : UserControl
{
    private readonly MainWindow _mainWindow;
    private readonly SupplierRepository _supplierRepository;
    private readonly ObservableCollection<Supplier> _suppliers = new();
    private Supplier? _selectedSupplier;
    private Supplier? _subscribedSupplier;
    private bool _isLoadingData;

    public SupplierWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _supplierRepository = RepositoryProvider.SupplierRepository;

        SupplierListBox.ItemsSource = _suppliers;
        LoadSuppliers();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        _mainWindow.ShowDashboard();

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddSupplierDialog { Owner = _mainWindow };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SupplierName))
            return;

        var name = dialog.SupplierName!.Trim();
        var exists = _suppliers.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            MessageBox.Show(_mainWindow,"اسم المورد موجود مسبقاً. يرجى اختيار اسم آخر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var supplier = _supplierRepository.AddSupplier(name);
        _suppliers.Add(supplier);
        SupplierListBox.SelectedItem = supplier;
    }

    private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        var supplier = SupplierListBox.SelectedItem as Supplier;
        if (supplier == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار مورد لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(_mainWindow,
            $"هل أنت متأكد من حذف المورد \"{supplier.Name}\"؟",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var idx = _suppliers.IndexOf(supplier);
        _supplierRepository.DeleteSupplier(supplier.SupplierId);
        _suppliers.Remove(supplier);

        if (_suppliers.Count > 0)
            SupplierListBox.SelectedIndex = Math.Min(idx, _suppliers.Count - 1);
        else
            ShowDetailsPanel(false);
    }

    private void SupplierListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UnsubscribeFromSupplier(_subscribedSupplier);
        _subscribedSupplier = null;

        _selectedSupplier = SupplierListBox.SelectedItem as Supplier;
        if (_selectedSupplier == null)
        {
            ShowDetailsPanel(false);
            return;
        }

        ShowDetailsPanel(true);
        DetailsPanel.DataContext = _selectedSupplier;
        SupplierNameText.Text = _selectedSupplier.Name;
        EntriesDataGrid.ItemsSource = _selectedSupplier.Entries;

        UpdateTotals();

        SubscribeToSupplier(_selectedSupplier);
        _subscribedSupplier = _selectedSupplier;
    }

    private void SubscribeToSupplier(Supplier? supplier)
    {
        if (supplier == null) return;
        supplier.Entries.CollectionChanged += OnEntriesChanged;
        foreach (var entry in supplier.Entries)
            entry.PropertyChanged += OnEntryPropertyChanged;
    }

    private void UnsubscribeFromSupplier(Supplier? supplier)
    {
        if (supplier == null) return;
        supplier.Entries.CollectionChanged -= OnEntriesChanged;
        foreach (var entry in supplier.Entries)
            entry.PropertyChanged -= OnEntryPropertyChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is ObservableCollection<SupplierEntry> entries && _subscribedSupplier?.Entries == entries)
        {
            if (e.NewItems != null)
            {
                foreach (SupplierEntry entry in e.NewItems)
                {
                    entry.SupplierId = _subscribedSupplier.SupplierId;
                    entry.PropertyChanged += OnEntryPropertyChanged;
                    if (!_isLoadingData && entry.EntryId == 0)
                        _supplierRepository.AddEntry(_subscribedSupplier.SupplierId, entry);
                }
            }
            if (e.OldItems != null)
            {
                foreach (SupplierEntry entry in e.OldItems)
                {
                    entry.PropertyChanged -= OnEntryPropertyChanged;
                    if (!_isLoadingData && entry.EntryId > 0)
                        _supplierRepository.DeleteEntry(entry.EntryId);
                }
            }
        }
        UpdateTotals();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoadingData && sender is SupplierEntry entry && entry.EntryId > 0)
            _supplierRepository.UpdateEntry(entry);
        UpdateTotals();
    }

    private void ShowDetailsPanel(bool visible)
    {
        DetailsPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PrintButton.IsEnabled = visible;
        SavePdfButton.IsEnabled = visible;
        if (!visible)
        {
            DetailsPanel.DataContext = null;
            EntriesDataGrid.ItemsSource = null;
            SupplierNameText.Text = string.Empty;
            TotalPriceText.Text = "—";
            TotalPaidText.Text = "—";
            TotalRemainingText.Text = "—";
            TotalQuantityText.Text = "—";
        }
    }

    private void AddReceiptImages_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null) return;

        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "الصور|*.jpg;*.jpeg;*.png;*.bmp;*.gif|جميع الملفات|*.*",
            Title = "اختيار صور الإيصال"
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            var full = Path.GetFullPath(path);
            if (_selectedSupplier.ReceiptImagePaths.Any(p => string.Equals(Path.GetFullPath(p), full, StringComparison.OrdinalIgnoreCase)))
                continue;
            _selectedSupplier.ReceiptImagePaths.Add(path);
            if (!_isLoadingData)
                _supplierRepository.AddReceipt(_selectedSupplier.SupplierId, path);
        }
    }

    private void RemoveSelectedReceiptImages_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null) return;
        if (ReceiptImagesList.SelectedItems.Count == 0)
        {
            MessageBox.Show(_mainWindow,"يرجى تحديد صورة أو أكثر لإزالتها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var toRemove = ReceiptImagesList.SelectedItems.Cast<string>().ToList();
        foreach (var path in toRemove)
        {
            if (!_isLoadingData)
                _supplierRepository.RemoveReceipt(_selectedSupplier.SupplierId, path);
            _selectedSupplier.ReceiptImagePaths.Remove(path);
        }
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null) return;
        _selectedSupplier.Entries.Add(new SupplierEntry { PurchaseDate = DateTime.Now, SupplierId = _selectedSupplier.SupplierId });
        UpdateTotals();
    }

    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null) return;
        var entry = EntriesDataGrid.SelectedItem as SupplierEntry;
        if (entry == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار صف لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _selectedSupplier.Entries.Remove(entry);
        UpdateTotals();
    }

    private void LoadSuppliers()
    {
        _isLoadingData = true;
        try
        {
            _suppliers.Clear();
            foreach (var supplier in _supplierRepository.GetAllSuppliers())
                _suppliers.Add(supplier);
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    private void UpdateTotals()
    {
        if (_selectedSupplier == null) return;

        var totalPrice = _selectedSupplier.Entries.Sum(e => e.TotalPrice);
        var totalPaid = _selectedSupplier.Entries.Sum(e => e.Paid);
        var totalRemaining = _selectedSupplier.Entries.Sum(e => e.Remaining);
        var totalQty = _selectedSupplier.Entries.Sum(e => e.QuantityKg);

        var inv = CultureInfo.InvariantCulture;
        TotalPriceText.Text = totalPrice.ToString("N2", inv);
        TotalPaidText.Text = totalPaid.ToString("N2", inv);
        TotalRemainingText.Text = totalRemaining.ToString("N2", inv);
        TotalQuantityText.Text = totalQty.ToString("N2", inv);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار مورد أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SupplierReportHelper.Print(_selectedSupplier);
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار مورد أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "ملف PDF|*.pdf|جميع الملفات|*.*",
            DefaultExt = "pdf",
            FileName = $"مورد_{_selectedSupplier.Name}_{DateTime.Now:yyyyMMdd}.pdf",
            Title = "حفظ كـ PDF"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SupplierReportHelper.GeneratePdf(_selectedSupplier, dlg.FileName);
            MessageBox.Show(_mainWindow,"تم حفظ الملف بنجاح.", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,"حدث خطأ أثناء الحفظ: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
