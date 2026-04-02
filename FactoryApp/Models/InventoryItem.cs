using System.ComponentModel;

namespace FactoryApp.Models;

public class InventoryItem : INotifyPropertyChanged
{
    private string _ddid = string.Empty;
    private string _name = string.Empty;
    private string? _photoPath;
    private decimal _produced;
    private decimal _availableInStorage;
    private decimal _totalDeliveredToCustomers;
    private decimal _totalOrderedOnCustomerReceipts;

    public string Ddid
    {
        get => _ddid;
        set { _ddid = value ?? string.Empty; OnPropertyChanged(nameof(Ddid)); }
    }

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(nameof(Name)); }
    }

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

    /// <summary>كمية الإنتاج المُسجَّلة؛ زيادتها تُضاف إلى المتوفر.</summary>
    public decimal Produced
    {
        get => _produced;
        set
        {
            if (_produced == value) return;
            _produced = value;
            OnPropertyChanged(nameof(Produced));
        }
    }

    public decimal AvailableInStorage
    {
        get => _availableInStorage;
        set
        {
            if (_availableInStorage == value) return;
            _availableInStorage = value;
            OnPropertyChanged(nameof(AvailableInStorage));
        }
    }

    /// <summary>Sum of delivery-section quantities for this DDID across all customers (reduces المتوفر).</summary>
    public decimal TotalDeliveredToCustomers
    {
        get => _totalDeliveredToCustomers;
        set
        {
            if (_totalDeliveredToCustomers == value) return;
            _totalDeliveredToCustomers = value;
            OnPropertyChanged(nameof(TotalDeliveredToCustomers));
            OnPropertyChanged(nameof(CurrentOutstandingCustomerNeed));
        }
    }

    /// <summary>طلبات العملاء — مجموع كمية الإيصال لكل العملاء لهذا DDID.</summary>
    public decimal TotalOrderedOnCustomerReceipts
    {
        get => _totalOrderedOnCustomerReceipts;
        set
        {
            if (_totalOrderedOnCustomerReceipts == value) return;
            _totalOrderedOnCustomerReceipts = value;
            OnPropertyChanged(nameof(TotalOrderedOnCustomerReceipts));
            OnPropertyChanged(nameof(CurrentOutstandingCustomerNeed));
        }
    }

    /// <summary>الاحتياج: طلبات العملاء − مسلّم للعملاء.</summary>
    public decimal CurrentOutstandingCustomerNeed =>
        TotalOrderedOnCustomerReceipts - TotalDeliveredToCustomers;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
