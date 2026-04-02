using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using FactoryApp.Models;
using FactoryApp.Repositories;
using FactoryApp.Services;

namespace FactoryApp;

public partial class InventoryWindow : UserControl
{
    private readonly MainWindow _mainWindow;
    private readonly InventoryRepository _inventoryRepository = RepositoryProvider.InventoryRepository;
    private readonly ObservableCollection<InventoryItem> _items = new();
    private bool _isLoading;

    public InventoryWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        InventoryDataGrid.ItemsSource = _items;
        Loaded += (_, _) => LoadInventoryItems();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        _mainWindow.ShowDashboard();

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<InventoryItem> snapshot;
        try
        {
            snapshot = _inventoryRepository.GetInventoryItems();
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر تحميل بيانات المخزن.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new InventoryProductionDialog(_inventoryRepository, snapshot) { Owner = _mainWindow };
        if (dialog.ShowDialog() == true)
            LoadInventoryItems();
    }

    private void PrintInventory_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Path.GetTempPath(), $"FactoryApp_InventoryPrint_{Guid.NewGuid():N}.pdf");
        try
        {
            InventoryReportHelper.GeneratePdf(_items.ToList(), path);
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

    private void SavePdfInventory_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"Inventory_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            InventoryReportHelper.GeneratePdf(_items.ToList(), dlg.FileName);
            MessageBox.Show(_mainWindow, $"تم حفظ الملف.\n{dlg.FileName}", "المخزن", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر حفظ PDF.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadInventoryItems()
    {
        _isLoading = true;
        try
        {
            _items.Clear();
            foreach (var item in _inventoryRepository.GetInventoryItems())
            {
                _items.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر تحميل بيانات المخزن.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
        }

        SyncPhotoPreview();
    }

    private void InventoryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => SyncPhotoPreview();

    private void InventoryThumbnailImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not InventoryItem item)
            return;
        InventoryDataGrid.SelectedItem = item;
        InventoryDataGrid.ScrollIntoView(item);
        SyncPhotoPreview();
        e.Handled = true;
    }

    private void SyncPhotoPreview()
    {
        if (PhotoPreviewPlaceholder == null)
            return;

        var item = InventoryDataGrid.SelectedItem as InventoryItem;
        if (item == null)
        {
            PhotoPreviewPlaceholder.Visibility = Visibility.Visible;
            PhotoPreviewScrollViewer.Visibility = Visibility.Collapsed;
            PhotoPreviewLargeImage.Source = null;
            return;
        }

        PhotoPreviewPlaceholder.Visibility = Visibility.Collapsed;
        PhotoPreviewScrollViewer.Visibility = Visibility.Visible;
        PhotoPreviewNameText.Text = string.IsNullOrWhiteSpace(item.Name) ? "—" : item.Name;
        PhotoPreviewDdidText.Text = string.IsNullOrWhiteSpace(item.Ddid) ? string.Empty : $"DDID: {item.Ddid}";

        var path = item.PhotoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PhotoPreviewLargeImage.Source = null;
            PhotoPreviewNoFileText.Visibility = Visibility.Visible;
            return;
        }

        PhotoPreviewNoFileText.Visibility = Visibility.Collapsed;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 720;
            bitmap.EndInit();
            bitmap.Freeze();
            PhotoPreviewLargeImage.Source = bitmap;
        }
        catch
        {
            PhotoPreviewLargeImage.Source = null;
            PhotoPreviewNoFileText.Visibility = Visibility.Visible;
        }
    }

    private void InventoryDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_isLoading)
            return;

        if (e.Row.Item is not InventoryItem item)
            return;
        if (e.Column is not DataGridTextColumn column || column.Header is not string header)
            return;
        if (e.EditingElement is not TextBox editor)
            return;

        var text = (editor.Text ?? string.Empty).Trim();

        if (header != "المتوفر في المخزن")
            return;

        if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var available) &&
            !decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out available))
        {
            MessageBox.Show(_mainWindow, "قيمة المتوفر في المخزن غير صحيحة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        item.AvailableInStorage = available;
        try
        {
            _inventoryRepository.UpsertAvailableInStorage(item.Ddid, available);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow, $"تعذر حفظ قيمة المتوفر.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
