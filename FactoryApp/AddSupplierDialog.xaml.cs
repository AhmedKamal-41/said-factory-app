using System.Windows;

namespace FactoryApp;

public partial class AddSupplierDialog : Window
{
    public string? SupplierName { get; private set; }

    public AddSupplierDialog()
    {
        InitializeComponent();
        Loaded += (s, e) => SupplierNameTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = SupplierNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("يرجى إدخال اسم المورد.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            SupplierNameTextBox.Focus();
            return;
        }

        SupplierName = name;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
