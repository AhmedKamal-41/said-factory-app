using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FactoryApp.Models;
using FactoryApp.Repositories;
using FactoryApp.Services;

namespace FactoryApp;

public partial class TreasuryWindow : UserControl
{
    private readonly MainWindow _mainWindow;
    private readonly TreasuryRepository _treasuryRepository;
    private readonly ObservableCollection<TreasuryEntry> _entries = new();
    private decimal _openingBalance;
    private bool _openingBalanceTextInitialized;
    private bool _suppressOpeningBalanceDirty;
    private bool _openingBalanceUserEdited;
    private bool _openingBalanceLocked;
    private bool _isLoadingData;

    public TreasuryWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _treasuryRepository = RepositoryProvider.TreasuryRepository;
        EntriesDataGrid.ItemsSource = _entries;
        _entries.CollectionChanged += OnEntriesCollectionChanged;
        LoadPersistedData();
        UpdateSummary();

        Loaded += (_, _) =>
        {
            if (!_openingBalanceTextInitialized)
            {
                _suppressOpeningBalanceDirty = true;
                try
                {
                    OpeningBalanceTextBox.Text = _openingBalance.ToString("N2", CultureInfo.CurrentCulture);
                    _openingBalanceTextInitialized = true;
                    if (_treasuryRepository.HasOpeningBalanceBeenSaved())
                        ApplyOpeningBalanceLocked();
                }
                finally
                {
                    _suppressOpeningBalanceDirty = false;
                }
            }
        };
    }

    private void OpeningBalanceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressOpeningBalanceDirty || _openingBalanceLocked)
            return;
        _openingBalanceUserEdited = true;
    }

    private void ApplyOpeningBalanceLocked()
    {
        _openingBalanceLocked = true;
        OpeningBalanceTextBox.IsReadOnly = true;
        OpeningBalanceTextBox.Focusable = false;
        OpeningBalanceTextBox.Cursor = System.Windows.Input.Cursors.Arrow;
        OpeningBalanceTextBox.ToolTip = "تم حفظ رصيد أول المدة ولا يمكن تعديله.";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        _mainWindow.ShowDashboard();

    private void AddRecord_Click(object sender, RoutedEventArgs e)
    {
        var entry = new TreasuryEntry
        {
            Date = DateTime.Today
        };
        _entries.Add(entry);
        EntriesDataGrid.SelectedItem = entry;
        EntriesDataGrid.ScrollIntoView(entry);
    }

    private void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesDataGrid.SelectedItem is not TreasuryEntry row)
        {
            MessageBox.Show(_mainWindow, "يرجى اختيار سجل لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _entries.Remove(row);
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        var totalAdded = _entries.Sum(x => x.AddedAmount);
        var totalTaken = _entries.Sum(x => x.TakenAmount);
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"Treasury_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            TreasuryReportHelper.GeneratePdf(_openingBalance, _entries.ToList(), totalAdded, totalTaken, dlg.FileName);
            MessageBox.Show(_mainWindow, $"تم حفظ الملف.\n{dlg.FileName}", "الخزنة", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر حفظ PDF.\n{ex.Message}", "الخزنة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var totalAdded = _entries.Sum(x => x.AddedAmount);
        var totalTaken = _entries.Sum(x => x.TakenAmount);
        try
        {
            TreasuryReportHelper.Print(_openingBalance, _entries.ToList(), totalAdded, totalTaken);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر إتمام الطباعة.\n{ex.Message}", "الخزنة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpeningBalanceTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_openingBalanceLocked)
            return;

        if (!TryParseDecimal(OpeningBalanceTextBox.Text, out var value))
        {
            MessageBox.Show(_mainWindow, "يرجى إدخال رقم صالح لرصيد أول المدة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            OpeningBalanceTextBox.Text = _openingBalance.ToString("N2", CultureInfo.CurrentCulture);
            return;
        }

        _openingBalance = value;
        OpeningBalanceTextBox.Text = _openingBalance.ToString("N2", CultureInfo.CurrentCulture);

        // Avoid locking on first visit when the user never edited (default 0.00 shown). Once saved or edited, persist and lock.
        var alreadySaved = _treasuryRepository.HasOpeningBalanceBeenSaved();
        if (_openingBalanceUserEdited || alreadySaved)
        {
            _treasuryRepository.SetOpeningBalance(_openingBalance);
            ApplyOpeningBalanceLocked();
        }

        RecalculateChain();
    }

    private void EntriesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not TreasuryEntry entry) return;
        if (e.Column is not DataGridTextColumn col) return;
        if (col.Binding is not System.Windows.Data.Binding binding || binding.Path.Path != nameof(TreasuryEntry.ActualAmountToday))
            return;
        if (e.EditingElement is not TextBox tb) return;

        var text = tb.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            entry.ActualAmountToday = 0;
            return;
        }

        if (!TryParseDecimal(text, out var value))
        {
            e.Cancel = true;
            MessageBox.Show(_mainWindow, "يرجى إدخال رقم صالح في «المبلغ الفعلي اليوم».", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (TreasuryEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnEntryPropertyChanged;
                if (!_isLoadingData && entry.EntryId == 0)
                    _treasuryRepository.AddEntry(entry);
            }
        }

        if (e.OldItems != null)
        {
            foreach (TreasuryEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
                if (!_isLoadingData && entry.EntryId > 0)
                    _treasuryRepository.DeleteEntry(entry.EntryId);
            }
        }

        RecalculateChain();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TreasuryEntry.ActualAmountToday))
            RecalculateChain();
        else if (!_isLoadingData && sender is TreasuryEntry entry && entry.EntryId > 0 &&
                 (e.PropertyName == nameof(TreasuryEntry.Date) || e.PropertyName == nameof(TreasuryEntry.Reason)))
            _treasuryRepository.UpdateEntry(entry);
    }

    /// <summary>
    /// Row order defines the chain: first row uses opening balance as previous; each next row uses prior row’s actual count.
    /// </summary>
    private void RecalculateChain()
    {
        decimal carry = _openingBalance;
        foreach (var entry in _entries)
        {
            entry.SetPreviousBalance(carry);
            carry = entry.ActualAmountToday;
        }

        if (!_isLoadingData)
            PersistAllEntries();

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var inv = CultureInfo.InvariantCulture;
        var totalAdded = _entries.Sum(x => x.AddedAmount);
        var totalTaken = _entries.Sum(x => x.TakenAmount);
        TotalAddedText.Text = totalAdded.ToString("N2", inv);
        TotalTakenText.Text = totalTaken.ToString("N2", inv);
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        text = text.Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }

    private void LoadPersistedData()
    {
        _isLoadingData = true;
        try
        {
            _openingBalance = _treasuryRepository.GetOpeningBalance();
            _entries.Clear();
            foreach (var entry in _treasuryRepository.GetAllEntries())
                _entries.Add(entry);
        }
        finally
        {
            _isLoadingData = false;
        }

        RecalculateChain();
    }

    private void PersistAllEntries()
    {
        foreach (var entry in _entries.Where(x => x.EntryId > 0))
            _treasuryRepository.UpdateEntry(entry);
    }
}
