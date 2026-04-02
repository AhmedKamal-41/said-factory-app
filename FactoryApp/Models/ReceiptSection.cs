using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// One partial delivery for a sales receipt; each row deducts its quantity from المخزن when saved.
/// </summary>
public class ReceiptSection : INotifyPropertyChanged
{
    public int SectionId { get; set; }
    public int ReceiptId { get; set; }

    private DateTime _deliveryDate = DateTime.Today;

    public DateTime DeliveryDate
    {
        get => _deliveryDate;
        set
        {
            var d = value.Date;
            if (_deliveryDate.Date == d) return;
            _deliveryDate = d;
            OnPropertyChanged(nameof(DeliveryDate));
        }
    }

    private decimal _quantity;

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged(nameof(Quantity));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
