using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// Represents a single entry (purchase record) for a supplier.
/// Database-ready: can be persisted to SQLite/PostgreSQL later.
/// </summary>
public class SupplierEntry : INotifyPropertyChanged
{
    public int EntryId { get; set; }
    public int SupplierId { get; set; }

    private DateTime _purchaseDate;
    private string _materialName = string.Empty;
    private decimal _quantityKg;
    private decimal _pricePerKg;
    private decimal _paid;
    private decimal _remaining;

    public DateTime PurchaseDate
    {
        get => _purchaseDate;
        set { _purchaseDate = value; OnPropertyChanged(nameof(PurchaseDate)); }
    }

    public string MaterialName
    {
        get => _materialName;
        set { _materialName = value ?? string.Empty; OnPropertyChanged(nameof(MaterialName)); }
    }

    public decimal QuantityKg
    {
        get => _quantityKg;
        set
        {
            if (_quantityKg == value) return;
            _quantityKg = value;
            OnPropertyChanged(nameof(QuantityKg));
            OnPropertyChanged(nameof(TotalPrice));
            RecalculateRemaining();
        }
    }

    /// <summary>سعر الكيلو</summary>
    public decimal PricePerKg
    {
        get => _pricePerKg;
        set
        {
            if (_pricePerKg == value) return;
            _pricePerKg = value;
            OnPropertyChanged(nameof(PricePerKg));
            OnPropertyChanged(nameof(TotalPrice));
            RecalculateRemaining();
        }
    }

    /// <summary>Total (سعرها) = الكمية × سعر الكيلو</summary>
    public decimal TotalPrice => _quantityKg * _pricePerKg;

    public decimal Paid
    {
        get => _paid;
        set
        {
            if (_paid == value) return;
            _paid = value;
            OnPropertyChanged(nameof(Paid));
            RecalculateRemaining();
        }
    }

    /// <summary>الباقي = Total - المدفوع</summary>
    public decimal Remaining
    {
        get => _remaining;
        private set { _remaining = value; OnPropertyChanged(nameof(Remaining)); }
    }

    private void RecalculateRemaining()
    {
        Remaining = TotalPrice - _paid;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
