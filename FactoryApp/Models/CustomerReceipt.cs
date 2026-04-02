using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// Sales receipt for a customer: line totals, partial deliveries (sections), and installment payments.
/// </summary>
public class CustomerReceipt : INotifyPropertyChanged
{
    public int ReceiptId { get; set; }

    private DateTime _date = DateTime.Today;
    private string _ddid = string.Empty;
    private string _kind = string.Empty;
    private decimal _quantity;
    private decimal _pricePerPiece;
    private decimal _discount;
    private decimal _deposit;
    private string? _productPhotoPath;

    public CustomerReceipt()
    {
        Payments = new ObservableCollection<CustomerPayment>();
        Payments.CollectionChanged += OnPaymentsCollectionChanged;
        Sections = new ObservableCollection<ReceiptSection>();
    }

    /// <summary>أقسام التسليم — كل سطر ينقص المخزن بكميته عند الحفظ.</summary>
    public ObservableCollection<ReceiptSection> Sections { get; }

    public DateTime Date
    {
        get => _date;
        set
        {
            var d = value.Date;
            if (_date == d) return;
            _date = d;
            OnPropertyChanged(nameof(Date));
        }
    }

    public string Ddid
    {
        get => _ddid;
        set
        {
            var n = value ?? string.Empty;
            if (_ddid == n) return;
            _ddid = n;
            OnPropertyChanged(nameof(Ddid));
        }
    }

    /// <summary>Resolved from Products by DDID for UI; not a separate persisted field.</summary>
    public string? ProductPhotoPath
    {
        get => _productPhotoPath;
        private set
        {
            if (_productPhotoPath == value) return;
            _productPhotoPath = value;
            OnPropertyChanged(nameof(ProductPhotoPath));
        }
    }

    /// <summary>Sets catalog photo path after lookup (typically from <c>IProductRepository.GetByDDID</c>).</summary>
    public void SetProductPhotoPath(string? path)
    {
        ProductPhotoPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }

    public string Kind
    {
        get => _kind;
        set { _kind = value ?? string.Empty; OnPropertyChanged(nameof(Kind)); }
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged(nameof(Quantity));
            NotifyTotalsChanged();
        }
    }

    public decimal PricePerPiece
    {
        get => _pricePerPiece;
        set
        {
            if (_pricePerPiece == value) return;
            _pricePerPiece = value;
            OnPropertyChanged(nameof(PricePerPiece));
            NotifyTotalsChanged();
        }
    }

    /// <summary>Discount per piece; defaults to 0. Still applied when discount column is hidden.</summary>
    public decimal Discount
    {
        get => _discount;
        set
        {
            if (_discount == value) return;
            _discount = value;
            OnPropertyChanged(nameof(Discount));
            NotifyTotalsChanged();
        }
    }

    /// <summary>العربون (deposit).</summary>
    public decimal Deposit
    {
        get => _deposit;
        set
        {
            if (_deposit == value) return;
            _deposit = value;
            OnPropertyChanged(nameof(Deposit));
            NotifyTotalsChanged();
        }
    }

    /// <summary>الإجمالي = الكمية × (سعر القطعة - خصم القطعة)</summary>
    public decimal Total => Quantity * (PricePerPiece - Discount);

    /// <summary>المتبقي = الإجمالي - العربون</summary>
    public decimal Remaining => Total - Deposit;

    public ObservableCollection<CustomerPayment> Payments { get; }

    private void OnPaymentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CustomerPayment p in e.NewItems)
            {
                p.OwnerReceipt = this;
                p.PropertyChanged += OnPaymentPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (CustomerPayment p in e.OldItems)
            {
                p.PropertyChanged -= OnPaymentPropertyChanged;
                p.OwnerReceipt = null;
            }
        }

        RecalculateInstallments();
    }

    private void OnPaymentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CustomerPayment.Amount) or nameof(CustomerPayment.IsPaid))
            RecalculateInstallments();
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Remaining));
        RecalculateInstallments();
    }

    /// <summary>Chain: starts from receipt <see cref="Remaining"/>; each row updates المتبقي بعد الدفع.</summary>
    public void RecalculateInstallments()
    {
        decimal running = Remaining;
        foreach (var p in Payments)
        {
            if (p.IsPaid)
                running -= p.Amount;
            p.RemainingAfterPayment = running;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
