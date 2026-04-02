using System.ComponentModel;

namespace FactoryApp.Models;

public class ReceiptImageItem : INotifyPropertyChanged
{
    private string _imagePath = string.Empty;
    private DateTime? _uploadedDate;

    public string ImagePath
    {
        get => _imagePath;
        set
        {
            var v = value ?? string.Empty;
            if (_imagePath == v) return;
            _imagePath = v;
            OnPropertyChanged(nameof(ImagePath));
        }
    }

    public DateTime? UploadedDate
    {
        get => _uploadedDate;
        set
        {
            if (_uploadedDate == value) return;
            _uploadedDate = value;
            OnPropertyChanged(nameof(UploadedDate));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
