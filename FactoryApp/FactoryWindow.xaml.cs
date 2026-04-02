using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FactoryApp.Models;
using FactoryApp.Repositories;
using FactoryApp.Services;

namespace FactoryApp;

public partial class FactoryWindow : UserControl
{
    private readonly MainWindow _mainWindow;
    private readonly FactoryRepository _factoryRepository;
    private readonly ObservableCollection<FactoryEntry> _entries = new();
    private bool _isLoadingRows;

    public FactoryWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _factoryRepository = RepositoryProvider.FactoryRepository;
        EntriesDataGrid.ItemsSource = _entries;
        _entries.CollectionChanged += OnEntriesCollectionChanged;
        LoadPersistedRows();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        _mainWindow.ShowDashboard();

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        _entries.Add(new FactoryEntry());
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        var row = EntriesDataGrid.SelectedItem as FactoryEntry;
        if (row == null)
        {
            MessageBox.Show(_mainWindow, "يرجى اختيار صف لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _entries.Remove(row);
    }

    private void PrintFactory_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Path.GetTempPath(), $"FactoryApp_FactoryPrint_{Guid.NewGuid():N}.pdf");
        try
        {
            FactoryReportHelper.GeneratePdf(_entries.ToList(), path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر إنشاء ملف للطباعة.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,
                $"تعذر إرسال الملف للطباعة تلقائياً.\n{ex.Message}\n\nالملف محفوظ هنا:\n{path}\nافتحه واختر طباعة يدوياً.",
                "تنبيه",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void SavePdfFactory_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"Factory_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            FactoryReportHelper.GeneratePdf(_entries.ToList(), dlg.FileName);
            MessageBox.Show(_mainWindow, $"تم حفظ الملف.\n{dlg.FileName}", "حسابات المصنع", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر حفظ PDF.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (FactoryEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnEntryPropertyChanged;
                if (!_isLoadingRows && entry.EntryId == 0)
                    _factoryRepository.AddEntry(entry);
            }
        }
        if (e.OldItems != null)
        {
            foreach (FactoryEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
                if (!_isLoadingRows && entry.EntryId > 0)
                    _factoryRepository.DeleteEntry(entry.EntryId);
            }
        }
        UpdateTotals();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoadingRows && sender is FactoryEntry changedEntry && changedEntry.EntryId > 0)
            _factoryRepository.UpdateEntry(changedEntry);

        if (e.PropertyName is nameof(FactoryEntry.Ddid) or nameof(FactoryEntry.Name) or nameof(FactoryEntry.PhotoPath))
            return;
        UpdateTotals();
    }

    private void LoadPersistedRows()
    {
        _isLoadingRows = true;
        try
        {
            _entries.Clear();
            foreach (var row in _factoryRepository.GetAllEntries())
                _entries.Add(row);
        }
        finally
        {
            _isLoadingRows = false;
        }
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var totalPrice = _entries.Sum(x => x.TotalPrice);
        var totalShirts = _entries.Sum(x => x.ShirtCount);
        var totalMfg = _entries.Sum(x => x.ManufacturingCost);
        var totalWaste = _entries.Sum(x => x.Waste);
        var totalPrinting = _entries.Sum(x => x.PrintingCost);
        var totalCostPerShirt = _entries.Sum(x => x.CostPerShirt);
        var totalCostAll = _entries.Sum(x => x.TotalCostAll);

        var inv = CultureInfo.InvariantCulture;
        TotalPriceSumText.Text = totalPrice.ToString("N2", inv);
        TotalShirtCountText.Text = totalShirts.ToString("N2", inv);
        TotalManufacturingText.Text = totalMfg.ToString("N2", inv);
        TotalWasteText.Text = totalWaste.ToString("N2", inv);
        TotalPrintingText.Text = totalPrinting.ToString("N2", inv);
        TotalCostPerShirtSumText.Text = totalCostPerShirt.ToString("N2", inv);
        TotalCostAllSumText.Text = totalCostAll.ToString("N2", inv);
    }
}
