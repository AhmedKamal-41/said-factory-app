using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using FactoryApp.Models;
using FactoryApp.Repositories;
using FactoryApp.Services;

namespace FactoryApp;

public partial class CustomerWindow : UserControl
{
    private sealed class AggregatedSectionRow(CustomerReceipt receipt, ReceiptSection section, decimal quantityRemaining)
    {
        public CustomerReceipt Receipt { get; } = receipt;
        public ReceiptSection Section { get; } = section;
        public decimal QuantityRemaining { get; } = quantityRemaining;
    }

    private sealed class AggregatedPaymentRow(CustomerReceipt receipt, CustomerPayment payment, decimal displayRemainingAfter)
    {
        public CustomerReceipt Receipt { get; } = receipt;
        public CustomerPayment Payment { get; } = payment;
        public decimal DisplayRemainingAfter { get; } = displayRemainingAfter;
    }

    private readonly MainWindow _mainWindow;
    private readonly IProductRepository _productRepository = ProductRepositoryProvider.Instance;
    private readonly CustomerRepository _customerRepository = RepositoryProvider.CustomerRepository;
    private readonly ObservableCollection<Customer> _customers = new();
    private readonly ObservableCollection<AggregatedSectionRow> _aggregatedSections = new();
    private readonly ObservableCollection<AggregatedPaymentRow> _aggregatedPayments = new();
    private Customer? _selectedCustomer;
    private Customer? _subscribedCustomer;
    private bool _isLoadingData;

    public CustomerWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        CustomerListBox.ItemsSource = _customers;
        Loaded += (_, _) =>
        {
            SectionsDataGrid.ItemsSource = _aggregatedSections;
            PaymentsDataGrid.ItemsSource = _aggregatedPayments;
            ApplyDiscountColumnVisibility();
            LoadCustomers();
        };
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.ShowDashboard();
    }

    private void PrintCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null)
            return;

        var scopeDlg = new CustomerExportScopeDialog { Owner = _mainWindow };
        if (scopeDlg.ShowDialog() != true || scopeDlg.SelectedScope is not { } scope)
            return;

        var showDiscount = ShowDiscountCheckBox.IsChecked == true;
        var path = Path.Combine(Path.GetTempPath(), $"FactoryApp_CustomerPrint_{Guid.NewGuid():N}.pdf");
        try
        {
            CustomerReportHelper.GeneratePdf(_selectedCustomer, scope, showDiscount, path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر إنشاء ملف للطباعة.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void SavePdfCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null)
            return;

        var scopeDlg = new CustomerExportScopeDialog { Owner = _mainWindow };
        if (scopeDlg.ShowDialog() != true || scopeDlg.SelectedScope is not { } scope)
            return;

        var safeName = SanitizeFileName(_selectedCustomer.Name);
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"{safeName}_{scope switch
            {
                CustomerExportScope.Receipts => "ايصالات",
                CustomerExportScope.DeliverySections => "تسليم",
                CustomerExportScope.Payments => "دفعات",
                _ => "كامل"
            }}.pdf"
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var showDiscount = ShowDiscountCheckBox.IsChecked == true;
            CustomerReportHelper.GeneratePdf(_selectedCustomer, scope, showDiscount, dlg.FileName);
            MessageBox.Show(_mainWindow,"تم حفظ ملف PDF.", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر حفظ PDF.\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var t = string.IsNullOrWhiteSpace(name) ? "عميل" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            t = t.Replace(c, '_');
        return t.Length > 80 ? t[..80] : t;
    }

    private void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddCustomerDialog { Owner = _mainWindow };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CustomerName))
            return;

        var name = dialog.CustomerName!.Trim();
        if (_customers.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(_mainWindow,"اسم العميل موجود مسبقاً. يرجى اختيار اسم آخر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var customer = _customerRepository.AddCustomer(name);
            _customers.Add(customer);
            CustomerListBox.SelectedItem = customer;
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر إضافة العميل.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        var customer = CustomerListBox.SelectedItem as Customer;
        if (customer == null)
        {
            MessageBox.Show(_mainWindow,"يرجى اختيار عميلاً لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(_mainWindow,
            $"هل أنت متأكد من حذف العميل \"{customer.Name}\"؟",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            var idx = _customers.IndexOf(customer);
            _customerRepository.DeleteCustomer(customer.CustomerId);
            UnsubscribeFromCustomer(_subscribedCustomer);
            _subscribedCustomer = null;
            _customers.Remove(customer);

            if (_customers.Count > 0)
                CustomerListBox.SelectedIndex = Math.Min(idx, _customers.Count - 1);
            else
                ShowDetailsPanel(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر حذف العميل.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CustomerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UnsubscribeFromCustomer(_subscribedCustomer);
        _subscribedCustomer = null;

        _selectedCustomer = CustomerListBox.SelectedItem as Customer;
        if (_selectedCustomer == null)
        {
            ShowDetailsPanel(false);
            return;
        }

        ShowDetailsPanel(true);
        CustomerNameText.Text = _selectedCustomer.Name;
        ReceiptsDataGrid.ItemsSource = _selectedCustomer.Receipts;

        SubscribeToCustomer(_selectedCustomer);
        _subscribedCustomer = _selectedCustomer;
        foreach (var r in _selectedCustomer.Receipts)
            UpdateReceiptProductPhoto(r);
        UpdateSummary();

        RefreshReceiptDependentChrome();
    }

    private void ShowDetailsPanel(bool visible)
    {
        DetailsPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PrintCustomerButton.IsEnabled = visible;
        SavePdfCustomerButton.IsEnabled = visible;
        if (!visible)
        {
            ReceiptsDataGrid.ItemsSource = null;
            _aggregatedSections.Clear();
            _aggregatedPayments.Clear();
            CustomerNameText.Text = string.Empty;
            ResetSectionsDeliveryAreaChrome();
            RefreshProductPhotoContext();
        }
    }

    private void ReceiptsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshReceiptDependentChrome();
    }

    private void UpdateReceiptProductPhoto(CustomerReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.Ddid))
        {
            receipt.SetProductPhotoPath(null);
            receipt.Kind = string.Empty;
            return;
        }

        var product = _productRepository.GetByDDID(receipt.Ddid);
        receipt.SetProductPhotoPath(product?.PhotoPath);
        receipt.Kind = product?.Name ?? string.Empty;
    }

    /// <summary>Receipt explicitly selected in «إيصالات المبيعات» — used for +تسليم and +دفعة.</summary>
    private CustomerReceipt? GetSelectedReceiptForAdds()
    {
        if (ReceiptsDataGrid.SelectedItem is not CustomerReceipt r || r.ReceiptId <= 0)
            return null;
        return r;
    }

    private void RefreshProductPhotoContext()
    {
        if (ProductPhotoContextBorder == null) return;
        if (_selectedCustomer == null || ReceiptsDataGrid.ItemsSource == null)
        {
            ProductPhotoContextBorder.DataContext = null;
            return;
        }

        ProductPhotoContextBorder.DataContext = ReceiptsDataGrid.SelectedItem as CustomerReceipt;
    }

    private void RefreshReceiptDependentChrome()
    {
        RefreshProductPhotoContext();
        UpdateAggregatedChrome();
    }

    private void RefreshAggregatedSectionsAndPayments()
    {
        _aggregatedSections.Clear();
        _aggregatedPayments.Clear();

        if (_selectedCustomer == null)
        {
            UpdateAggregatedPlaceholderVisibility();
            UpdateSectionsDeliveryChrome();
            return;
        }

        var receipts = _selectedCustomer.Receipts.ToList();

        foreach (var r in receipts.OrderBy(x => x.ReceiptId))
        {
            var deliveredOnReceipt = r.Sections.Sum(s => s.Quantity);
            var remainingOnReceipt = r.Quantity - deliveredOnReceipt;
            foreach (var s in r.Sections)
                _aggregatedSections.Add(new AggregatedSectionRow(r, s, remainingOnReceipt));
        }

        decimal running = _selectedCustomer.Receipts.Sum(re => re.Total - re.Deposit);
        var ordered = _selectedCustomer.Receipts
            .SelectMany(re => re.Payments.Select(p => (Receipt: re, Payment: p)))
            .OrderBy(x => x.Payment.PaymentDate)
            .ThenBy(x => x.Receipt.ReceiptId)
            .ThenBy(x => x.Payment.PaymentId)
            .ToList();

        foreach (var x in ordered)
        {
            if (x.Payment.IsPaid)
                running -= x.Payment.Amount;
            _aggregatedPayments.Add(new AggregatedPaymentRow(x.Receipt, x.Payment, running));
        }

        UpdateAggregatedPlaceholderVisibility();
        UpdateSectionsDeliveryChrome();
    }

    private void ApplyInnerDeliveryCardIdle()
    {
        SectionsDeliveryInnerCard.Background = Brushes.White;
        SectionsDeliveryInnerCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        SectionsDeliveryInnerCard.BorderThickness = new Thickness(1);
        SectionsDeliveryInnerCard.Padding = new Thickness(16, 14, 16, 14);
        SectionsDeliveryInnerCard.CornerRadius = new CornerRadius(10);
    }

    private void ApplyInnerDeliveryCardComplete()
    {
        SectionsDeliveryInnerCard.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9));
        SectionsDeliveryInnerCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
        SectionsDeliveryInnerCard.BorderThickness = new Thickness(2);
        SectionsDeliveryInnerCard.Padding = new Thickness(16, 14, 16, 14);
        SectionsDeliveryInnerCard.CornerRadius = new CornerRadius(10);
    }

    private void SetDeliveryStatusChipInProgress()
    {
        SectionsStatusChipBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1));
        SectionsStatusChipBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x82));
        SectionsStatusChipText.Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
        SectionsStatusChipText.Text = "قيد التسليم";
    }

    private void SetDeliveryStatusChipComplete()
    {
        SectionsStatusChipBorder.Background = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
        SectionsStatusChipBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
        SectionsStatusChipText.Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
        SectionsStatusChipText.Text = "مكتمل";
    }

    private void ResetSectionsDeliveryAreaChrome()
    {
        SectionsCompletionRibbon.Visibility = Visibility.Collapsed;
        SectionsHeaderIconComplete.Visibility = Visibility.Collapsed;
        SectionsHeaderIconProgress.Visibility = Visibility.Visible;
        SectionsStatusChipBorder.Visibility = Visibility.Collapsed;
        ApplyInnerDeliveryCardIdle();
    }

    private void UpdateSectionsDeliveryChrome()
    {
        if (_selectedCustomer == null)
        {
            ResetSectionsDeliveryAreaChrome();
            return;
        }

        var receipts = _selectedCustomer.Receipts.ToList();
        if (receipts.Count == 0)
        {
            ApplyInnerDeliveryCardIdle();
            SectionsCompletionRibbon.Visibility = Visibility.Collapsed;
            SectionsStatusChipBorder.Visibility = Visibility.Collapsed;
            SectionsHeaderIconComplete.Visibility = Visibility.Collapsed;
            SectionsHeaderIconProgress.Visibility = Visibility.Visible;
            return;
        }

        SectionsStatusChipBorder.Visibility = Visibility.Visible;
        var totalOrdered = receipts.Sum(r => r.Quantity);
        if (totalOrdered <= 0m)
        {
            ApplyInnerDeliveryCardIdle();
            SectionsCompletionRibbon.Visibility = Visibility.Collapsed;
            SectionsHeaderIconComplete.Visibility = Visibility.Collapsed;
            SectionsHeaderIconProgress.Visibility = Visibility.Visible;
            SectionsStatusChipText.Text = "—";
            SectionsStatusChipText.Foreground = new SolidColorBrush(Color.FromRgb(0x5F, 0x63, 0x68));
            SectionsStatusChipBorder.Background = Brushes.White;
            SectionsStatusChipBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            return;
        }

        var totalDelivered = receipts.Sum(r => r.Sections.Sum(s => s.Quantity));
        var allReceived = totalDelivered >= totalOrdered;
        if (allReceived)
        {
            ApplyInnerDeliveryCardComplete();
            SetDeliveryStatusChipComplete();
            SectionsCompletionRibbon.Visibility = Visibility.Visible;
            SectionsHeaderIconComplete.Visibility = Visibility.Visible;
            SectionsHeaderIconProgress.Visibility = Visibility.Collapsed;
        }
        else
        {
            ApplyInnerDeliveryCardIdle();
            SetDeliveryStatusChipInProgress();
            SectionsCompletionRibbon.Visibility = Visibility.Collapsed;
            SectionsHeaderIconComplete.Visibility = Visibility.Collapsed;
            SectionsHeaderIconProgress.Visibility = Visibility.Visible;
        }
    }

    private void UpdateAggregatedPlaceholderVisibility()
    {
        if (_selectedCustomer == null)
        {
            SectionsDataGrid.Visibility = Visibility.Collapsed;
            SectionsPlaceholder.Visibility = Visibility.Collapsed;
            PaymentsDataGrid.Visibility = Visibility.Collapsed;
            PaymentsPlaceholder.Visibility = Visibility.Collapsed;
            return;
        }

        var hasSections = _aggregatedSections.Count > 0;
        var hasPayments = _aggregatedPayments.Count > 0;
        SectionsDataGrid.Visibility = hasSections ? Visibility.Visible : Visibility.Collapsed;
        SectionsPlaceholder.Visibility = hasSections ? Visibility.Collapsed : Visibility.Visible;
        PaymentsDataGrid.Visibility = hasPayments ? Visibility.Visible : Visibility.Collapsed;
        PaymentsPlaceholder.Visibility = hasPayments ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateAggregatedChrome()
    {
        var canAdd = _selectedCustomer != null && GetSelectedReceiptForAdds() != null;
        AddSectionButton.IsEnabled = canAdd;
        AddPaymentButton.IsEnabled = canAdd;
        DeleteSectionButton.IsEnabled = _selectedCustomer != null && SectionsDataGrid.SelectedItem is AggregatedSectionRow;
        DeletePaymentButton.IsEnabled = _selectedCustomer != null && PaymentsDataGrid.SelectedItem is AggregatedPaymentRow;
    }

    private void AggregatedSectionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAggregatedChrome();

    private void AggregatedPaymentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAggregatedChrome();

    private void AddReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        var receipt = new CustomerReceipt();
        _selectedCustomer.Receipts.Add(receipt);
        ReceiptsDataGrid.SelectedItem = receipt;
        ReceiptsDataGrid.ScrollIntoView(receipt);
        UpdateSummary();
    }

    private void DeleteReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;
        if (ReceiptsDataGrid.SelectedItem is not CustomerReceipt receipt)
        {
            MessageBox.Show(_mainWindow,"يرجى تحديد إيصال لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(_mainWindow,"حذف هذا الإيصال وجميع أقسام التسليم والدفعات؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        var idx = _selectedCustomer.Receipts.IndexOf(receipt);
        _selectedCustomer.Receipts.Remove(receipt);
        if (_selectedCustomer.Receipts.Count > 0)
            ReceiptsDataGrid.SelectedIndex = Math.Min(idx, _selectedCustomer.Receipts.Count - 1);
        else
            RefreshReceiptDependentChrome();
        UpdateSummary();
    }

    private void AddPayment_Click(object sender, RoutedEventArgs e)
    {
        var target = GetSelectedReceiptForAdds();
        if (target == null)
        {
            if (_selectedCustomer?.Receipts.Count == 0)
                MessageBox.Show(_mainWindow,"لا يوجد إيصال. أضف سطراً في جدول «إيصالات المبيعات» أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(_mainWindow,"يرجى تحديد إيصال (الصنف) من جدول «إيصالات المبيعات» أولاً، ثم اضغط «+ دفعة».", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var payment = new CustomerPayment();
        target.Payments.Add(payment);
        UpdateSummary();
        var payRow = _aggregatedPayments.FirstOrDefault(x => ReferenceEquals(x.Payment, payment));
        if (payRow != null)
        {
            PaymentsDataGrid.SelectedItem = payRow;
            PaymentsDataGrid.ScrollIntoView(payRow);
        }
        UpdateAggregatedChrome();
    }

    private void DeletePayment_Click(object sender, RoutedEventArgs e)
    {
        if (PaymentsDataGrid.SelectedItem is not AggregatedPaymentRow agg)
        {
            MessageBox.Show(_mainWindow,"يرجى تحديد دفعة لحذفها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var receipt = agg.Receipt;
        var idx = receipt.Payments.IndexOf(agg.Payment);
        receipt.Payments.Remove(agg.Payment);
        UpdateSummary();
        if (receipt.Payments.Count > 0 && idx >= 0)
        {
            var pick = receipt.Payments[Math.Min(idx, receipt.Payments.Count - 1)];
            var row = _aggregatedPayments.FirstOrDefault(x => ReferenceEquals(x.Payment, pick));
            if (row != null)
                PaymentsDataGrid.SelectedItem = row;
        }
        UpdateAggregatedChrome();
    }

    private void ShowDiscountCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ApplyDiscountColumnVisibility();
    }

    private void ApplyDiscountColumnVisibility()
    {
        var show = ShowDiscountCheckBox.IsChecked == true;
        if (DiscountColumn != null)
            DiscountColumn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SubscribeToCustomer(Customer? c)
    {
        if (c == null) return;
        c.Receipts.CollectionChanged += OnCustomerReceiptsCollectionChanged;
        foreach (var r in c.Receipts)
            SubscribeToReceipt(r);
    }

    private void UnsubscribeFromCustomer(Customer? c)
    {
        if (c == null) return;
        c.Receipts.CollectionChanged -= OnCustomerReceiptsCollectionChanged;
        foreach (var r in c.Receipts)
            UnsubscribeFromReceipt(r);
    }

    private void OnCustomerReceiptsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var customer = _subscribedCustomer;
        if (customer == null) return;

        if (e.NewItems != null)
        {
            foreach (CustomerReceipt r in e.NewItems)
            {
                SubscribeToReceipt(r);
                if (!_isLoadingData && r.ReceiptId == 0)
                    _customerRepository.AddReceipt(customer.CustomerId, r);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CustomerReceipt r in e.OldItems)
            {
                UnsubscribeFromReceipt(r);
                if (!_isLoadingData && r.ReceiptId > 0)
                    _customerRepository.DeleteReceipt(r.ReceiptId);
            }
        }

        UpdateSummary();
    }

    private void SubscribeToReceipt(CustomerReceipt r)
    {
        r.PropertyChanged += OnReceiptPropertyChanged;
        r.Payments.CollectionChanged += OnReceiptPaymentsCollectionChanged;
        r.Sections.CollectionChanged += OnReceiptSectionsCollectionChanged;
        foreach (var p in r.Payments)
            p.PropertyChanged += OnPaymentPropertyChangedForSummary;
        foreach (var s in r.Sections)
            s.PropertyChanged += OnReceiptSectionPropertyChanged;
        UpdateReceiptProductPhoto(r);
    }

    private void UnsubscribeFromReceipt(CustomerReceipt r)
    {
        r.PropertyChanged -= OnReceiptPropertyChanged;
        r.Payments.CollectionChanged -= OnReceiptPaymentsCollectionChanged;
        r.Sections.CollectionChanged -= OnReceiptSectionsCollectionChanged;
        foreach (var p in r.Payments)
            p.PropertyChanged -= OnPaymentPropertyChangedForSummary;
        foreach (var s in r.Sections)
            s.PropertyChanged -= OnReceiptSectionPropertyChanged;
    }

    private void OnReceiptPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CustomerReceipt receipt)
            return;

        if (e.PropertyName == nameof(CustomerReceipt.Ddid))
            UpdateReceiptProductPhoto(receipt);

        if (!_isLoadingData && _selectedCustomer != null && receipt.ReceiptId > 0)
            _customerRepository.UpdateReceipt(_selectedCustomer.CustomerId, receipt);
        UpdateSummary();
    }

    private void OnReceiptSectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var receipt = _subscribedCustomer?.Receipts.FirstOrDefault(r => ReferenceEquals(r.Sections, sender));
        if (receipt == null)
            return;

        if (e.NewItems != null)
        {
            foreach (ReceiptSection s in e.NewItems)
            {
                s.PropertyChanged += OnReceiptSectionPropertyChanged;
                if (!_isLoadingData && s.SectionId == 0 && receipt.ReceiptId > 0)
                    _customerRepository.AddReceiptSection(receipt.ReceiptId, receipt.Ddid, s);
            }
        }

        if (e.OldItems != null)
        {
            foreach (ReceiptSection s in e.OldItems)
            {
                s.PropertyChanged -= OnReceiptSectionPropertyChanged;
                if (!_isLoadingData && s.SectionId > 0)
                    _customerRepository.DeleteReceiptSection(receipt.ReceiptId, receipt.Ddid, s.SectionId);
            }
        }

        UpdateSummary();
    }

    private void OnReceiptSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ReceiptSection.Quantity) or nameof(ReceiptSection.DeliveryDate)))
            return;
        if (_isLoadingData || sender is not ReceiptSection sec || sec.SectionId == 0)
            return;
        var parent = _subscribedCustomer?.Receipts.FirstOrDefault(r => r.Sections.Contains(sec));
        if (parent == null || parent.ReceiptId == 0)
            return;
        try
        {
            _customerRepository.UpdateReceiptSection(parent.ReceiptId, parent.Ddid, sec);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر حفظ قسم التسليم.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        UpdateSummary();
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        var target = GetSelectedReceiptForAdds();
        if (target == null)
        {
            if (_selectedCustomer?.Receipts.Count == 0)
                MessageBox.Show(_mainWindow,"لا يوجد إيصال. أضف سطراً في جدول «إيصالات المبيعات» أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(_mainWindow,"يرجى تحديد إيصال (الصنف) من جدول «إيصالات المبيعات» أولاً، ثم اضغط «+ تسليم».", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var section = new ReceiptSection();
        target.Sections.Add(section);
        UpdateSummary();
        var sectionRow = _aggregatedSections.FirstOrDefault(x => ReferenceEquals(x.Section, section));
        if (sectionRow != null)
        {
            SectionsDataGrid.SelectedItem = sectionRow;
            SectionsDataGrid.ScrollIntoView(sectionRow);
        }
        UpdateAggregatedChrome();
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (SectionsDataGrid.SelectedItem is not AggregatedSectionRow agg)
        {
            MessageBox.Show(_mainWindow,"يرجى تحديد قسم تسليم لحذفه.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var receipt = agg.Receipt;
        var idx = receipt.Sections.IndexOf(agg.Section);
        receipt.Sections.Remove(agg.Section);
        UpdateSummary();
        if (receipt.Sections.Count > 0 && idx >= 0)
        {
            var pick = receipt.Sections[Math.Min(idx, receipt.Sections.Count - 1)];
            var row = _aggregatedSections.FirstOrDefault(x => ReferenceEquals(x.Section, pick));
            if (row != null)
                SectionsDataGrid.SelectedItem = row;
        }
        UpdateAggregatedChrome();
    }

    private void SectionsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_isLoadingData)
            return;
        if (e.Column is not DataGridTextColumn column || column.Header?.ToString() != "الكمية المُسلَّمة")
            return;
        if (e.EditingElement is not TextBox editor)
            return;

        var text = (editor.Text ?? string.Empty).Trim();
        if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) &&
            !decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out qty))
        {
            MessageBox.Show(_mainWindow,"الكمية غير صحيحة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        if (e.Row.Item is AggregatedSectionRow agg)
            agg.Section.Quantity = qty;
    }

    private void OnReceiptPaymentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var receipt = _subscribedCustomer?.Receipts.FirstOrDefault(r => ReferenceEquals(r.Payments, sender));
        if (receipt == null)
            return;

        if (e.NewItems != null)
        {
            foreach (CustomerPayment p in e.NewItems)
            {
                p.PropertyChanged += OnPaymentPropertyChangedForSummary;
                if (!_isLoadingData && p.PaymentId == 0 && receipt.ReceiptId > 0)
                    _customerRepository.AddPayment(receipt.ReceiptId, p);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CustomerPayment p in e.OldItems)
            {
                p.PropertyChanged -= OnPaymentPropertyChangedForSummary;
                if (!_isLoadingData && p.PaymentId > 0)
                    _customerRepository.DeletePayment(p.PaymentId);
            }
        }

        UpdateSummary();
    }

    private void OnPaymentPropertyChangedForSummary(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoadingData && sender is CustomerPayment payment && payment.PaymentId > 0)
        {
            var parent = _subscribedCustomer?.Receipts.FirstOrDefault(r => r.Payments.Contains(payment));
            if (parent != null && parent.ReceiptId > 0)
                _customerRepository.UpdatePayment(parent.ReceiptId, payment);
        }
        UpdateSummary();
    }

    private void LoadCustomers()
    {
        _isLoadingData = true;
        try
        {
            _customers.Clear();
            foreach (var customer in _customerRepository.GetAllCustomers())
            {
                foreach (var receipt in customer.Receipts)
                    UpdateReceiptProductPhoto(receipt);
                _customers.Add(customer);
            }
            if (_customers.Count > 0)
                CustomerListBox.SelectedIndex = 0;
            else
                ShowDetailsPanel(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_mainWindow,$"تعذر تحميل بيانات العملاء.\n{ex.Message}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoadingData = false;
        }
    }

    private void UpdateSummary()
    {
        if (_selectedCustomer == null)
        {
            ClearSummaryTexts();
            RefreshAggregatedSectionsAndPayments();
            return;
        }

        decimal totalQty = 0;
        decimal totalSales = 0;
        decimal totalPaid = 0;
        decimal totalDeposits = 0;

        foreach (var r in _selectedCustomer.Receipts)
        {
            totalQty += r.Quantity;
            totalSales += r.Total;
            totalDeposits += r.Deposit;
            totalPaid += r.Deposit;
            foreach (var p in r.Payments)
                if (p.IsPaid)
                    totalPaid += p.Amount;
        }

        var totalRemaining = totalSales - totalPaid;

        SummaryTotalQtyText.Text = totalQty.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        SummaryTotalSalesText.Text = totalSales.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        SummaryTotalPaidText.Text = totalPaid.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        SummaryTotalRemainingText.Text = totalRemaining.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        RefreshProductPhotoContext();
        RefreshAggregatedSectionsAndPayments();
    }

    private void ClearSummaryTexts()
    {
        SummaryTotalQtyText.Text = "—";
        SummaryTotalSalesText.Text = "—";
        SummaryTotalPaidText.Text = "—";
        SummaryTotalRemainingText.Text = "—";
    }
}
