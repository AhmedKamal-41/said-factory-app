using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// Customer account holder. In-memory; persistence can be added later.
/// </summary>
public class Customer : INotifyPropertyChanged
{
    public int CustomerId { get; set; }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(nameof(Name)); }
    }

    public ObservableCollection<CustomerReceipt> Receipts { get; }

    public Customer()
    {
        Receipts = new ObservableCollection<CustomerReceipt>();
    }

    public Customer(string name) : this()
    {
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
