using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FactoryApp.Models;
using FactoryApp.Services;
using Microsoft.Win32;

namespace FactoryApp;

public partial class DDIDEntryWindow : UserControl
{
    private readonly MainWindow _mainWindow;
    private readonly ObservableCollection<Product> _products = new();
    private readonly IProductRepository _repo = ProductRepositoryProvider.Instance;

    private Product? _selectedProduct;
    private string _selectedOriginalDdid = string.Empty;
    private string? _currentPhotoPath;

    public DDIDEntryWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        ProductsListBox.ItemsSource = _products;
        RefreshList();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        _mainWindow.ShowDashboard();

    private void RefreshList()
    {
        try
        {
            _products.Clear();
            foreach (var p in _repo.GetAllProducts())
                _products.Add(p);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر تحميل المنتجات من قاعدة البيانات.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ProductsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProduct = ProductsListBox.SelectedItem as Product;
        _selectedOriginalDdid = _selectedProduct?.DDID ?? string.Empty;

        if (_selectedProduct == null)
        {
            ClearFormFields(keepPhoto: false);
            return;
        }

        DdidTextBox.Text = _selectedProduct.DDID;
        NameTextBox.Text = _selectedProduct.Name;
        _currentPhotoPath = _selectedProduct.PhotoPath;
        UpdatePreview();
    }

    private void SelectPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "الصور|*.jpg;*.jpeg;*.png|جميع الملفات|*.*",
            Title = "اختيار صورة المنتج"
        };

        if (dlg.ShowDialog() != true)
            return;

        if (string.IsNullOrWhiteSpace(dlg.FileName) || !File.Exists(dlg.FileName))
        {
            MessageBox.Show(_mainWindow,"تعذر فتح الصورة المحددة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _currentPhotoPath = dlg.FileName;
        UpdatePreview();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var ddid = (DdidTextBox.Text ?? string.Empty).Trim();
        var name = (NameTextBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(ddid))
        {
            MessageBox.Show(_mainWindow,"يرجى إدخال DDID.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(_mainWindow,"يرجى إدخال الاسم.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Product? existingProduct;
        try
        {
            existingProduct = _repo.GetByDDID(ddid);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر التحقق من DDID.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (existingProduct != null)
        {
            MessageBox.Show(_mainWindow,"DDID موجود مسبقاً. يرجى استخدام DDID آخر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _repo.AddProduct(new Product
            {
                DDID = ddid,
                Name = name,
                PhotoPath = string.IsNullOrWhiteSpace(_currentPhotoPath) ? null : _currentPhotoPath
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر حفظ المنتج.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshList();
        SelectByDDID(ddid);
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار منتج أولاً من القائمة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var oldDdid = _selectedOriginalDdid;
        var newDdid = (DdidTextBox.Text ?? string.Empty).Trim();
        var newName = (NameTextBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(newDdid))
        {
            MessageBox.Show(_mainWindow,"يرجى إدخال DDID.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(_mainWindow,"يرجى إدخال الاسم.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isDdidChanged = !string.Equals(oldDdid, newDdid, StringComparison.OrdinalIgnoreCase);

        if (isDdidChanged)
        {
            Product? existing;
            try
            {
                existing = _repo.GetByDDID(newDdid);
            }
            catch (Exception ex)
            {
                MessageBox.Show(_mainWindow,$"تعذر التحقق من DDID الجديد.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (existing != null)
            {
                MessageBox.Show(_mainWindow,"DDID الجديد موجود مسبقاً. يرجى استخدام قيمة مختلفة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _repo.DeleteProduct(oldDdid);
                _repo.AddProduct(new Product
                {
                    DDID = newDdid,
                    Name = newName,
                    PhotoPath = string.IsNullOrWhiteSpace(_currentPhotoPath) ? null : _currentPhotoPath
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(_mainWindow,$"تعذر تحديث المنتج.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }
        else
        {
            try
            {
                _repo.UpdateProduct(new Product
                {
                    DDID = oldDdid,
                    Name = newName,
                    PhotoPath = string.IsNullOrWhiteSpace(_currentPhotoPath) ? null : _currentPhotoPath
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(_mainWindow,$"تعذر تحديث المنتج.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        RefreshList();
        SelectByDDID(newDdid);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار منتج لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ddid = _selectedOriginalDdid;
        var name = _selectedProduct.Name;

        var result = MessageBox.Show(_mainWindow,
            $"هل أنت متأكد من حذف المنتج \"{name}\"؟",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _repo.DeleteProduct(ddid);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر حذف المنتج.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshList();
        ClearFormFields(keepPhoto: false);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ProductsListBox.SelectedItem = null;
        ClearFormFields(keepPhoto: false);
    }

    private void ClearFormFields(bool keepPhoto)
    {
        DdidTextBox.Text = string.Empty;
        NameTextBox.Text = string.Empty;
        _selectedOriginalDdid = string.Empty;
        _selectedProduct = null;

        if (!keepPhoto)
            _currentPhotoPath = null;

        UpdatePreview();
    }

    private void SelectByDDID(string ddid)
    {
        if (string.IsNullOrWhiteSpace(ddid))
            return;

        for (var i = 0; i < _products.Count; i++)
        {
            if (string.Equals(_products[i].DDID, ddid, StringComparison.OrdinalIgnoreCase))
            {
                ProductsListBox.SelectedIndex = i;
                ProductsListBox.ScrollIntoView(_products[i]);
                return;
            }
        }
    }

    private void UpdatePreview()
    {
        var bitmap = LoadBitmapSafe(_currentPhotoPath);
        PreviewImage.Source = bitmap;
        PreviewNoPhotoText.Visibility = bitmap == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? LoadBitmapSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

