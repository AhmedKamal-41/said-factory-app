using System.Windows;

namespace FactoryApp;

public partial class AddCustomerDialog : Window
{
    public string? CustomerName { get; private set; }

    public AddCustomerDialog()
    {
        InitializeComponent();
        Loaded += (s, e) => CustomerNameTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = CustomerNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("يرجى إدخال اسم العميل.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            CustomerNameTextBox.Focus();
            return;
        }

        CustomerName = name;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
