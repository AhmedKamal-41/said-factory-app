using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// One row of factory accounting (حسابات المصنع). Database-ready for later persistence.
/// </summary>
public class FactoryEntry : INotifyPropertyChanged
{
    public int EntryId { get; set; }

    private string _ddid = string.Empty;
    private string _name = string.Empty;
    private string? _photoPath;
    private decimal _kilo;
    private decimal _pricePerKilo;
    private decimal _shirtCount;
    private decimal _manufacturingCost;
    private decimal _waste;
    private decimal _printingCost;

    public string Ddid
    {
        get => _ddid;
        set
        {
            var v = (value ?? string.Empty).Trim();
            if (_ddid == v) return;
            _ddid = v;
            OnPropertyChanged(nameof(Ddid));
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            var v = (value ?? string.Empty).Trim();
            if (_name == v) return;
            _name = v;
            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>Legacy DB column; not shown in UI.</summary>
    public string? PhotoPath
    {
        get => _photoPath;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_photoPath == v) return;
            _photoPath = v;
            OnPropertyChanged(nameof(PhotoPath));
        }
    }

    public decimal Kilo
    {
        get => _kilo;
        set
        {
            if (_kilo == value) return;
            _kilo = value;
            OnPropertyChanged(nameof(Kilo));
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(PricePerShirt));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    public decimal PricePerKilo
    {
        get => _pricePerKilo;
        set
        {
            if (_pricePerKilo == value) return;
            _pricePerKilo = value;
            OnPropertyChanged(nameof(PricePerKilo));
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(PricePerShirt));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    /// <summary>إجمالي السعر = كيلو × سعر الكيلو</summary>
    public decimal TotalPrice => _kilo * _pricePerKilo;

    public decimal ShirtCount
    {
        get => _shirtCount;
        set
        {
            if (_shirtCount == value) return;
            _shirtCount = value;
            OnPropertyChanged(nameof(ShirtCount));
            OnPropertyChanged(nameof(PricePerShirt));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    /// <summary>سعر القطعة = إجمالي السعر ÷ عدد التي شيرت (when count &gt; 0).</summary>
    public decimal PricePerShirt => _shirtCount == 0 ? 0m : TotalPrice / _shirtCount;

    public decimal ManufacturingCost
    {
        get => _manufacturingCost;
        set
        {
            if (_manufacturingCost == value) return;
            _manufacturingCost = value;
            OnPropertyChanged(nameof(ManufacturingCost));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    public decimal Waste
    {
        get => _waste;
        set
        {
            if (_waste == value) return;
            _waste = value;
            OnPropertyChanged(nameof(Waste));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    /// <summary>طباعة — entered manually.</summary>
    public decimal PrintingCost
    {
        get => _printingCost;
        set
        {
            if (_printingCost == value) return;
            _printingCost = value;
            OnPropertyChanged(nameof(PrintingCost));
            OnPropertyChanged(nameof(CostPerShirt));
            OnPropertyChanged(nameof(TotalCostAll));
        }
    }

    /// <summary>تكلفة التي شيرت = سعر القطعة + المصنعية + الهالك + طباعة</summary>
    public decimal CostPerShirt => PricePerShirt + _manufacturingCost + _waste + _printingCost;

    /// <summary>التكلفة الكل = تكلفة التي شيرت × عدد التي شيرت</summary>
    public decimal TotalCostAll => CostPerShirt * _shirtCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
