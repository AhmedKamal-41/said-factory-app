using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// Represents a supplier in the factory system.
/// Database-ready: can be persisted to SQLite/PostgreSQL later.
/// </summary>
public class Supplier : INotifyPropertyChanged
{
    public int SupplierId { get; set; }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(nameof(Name)); }
    }

    /// <summary>Paths to receipt image files (one or more). Empty when none attached.</summary>
    public ObservableCollection<string> ReceiptImagePaths { get; }

    public ObservableCollection<SupplierEntry> Entries { get; }

    public Supplier()
    {
        ReceiptImagePaths = new ObservableCollection<string>();
        Entries = new ObservableCollection<SupplierEntry>();
    }

    public Supplier(string name) : this()
    {
        Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
