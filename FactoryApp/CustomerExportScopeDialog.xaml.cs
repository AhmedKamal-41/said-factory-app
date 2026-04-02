using System.Windows;

namespace FactoryApp;

public partial class CustomerExportScopeDialog : Window
{
    public CustomerExportScope? SelectedScope { get; private set; }

    public CustomerExportScopeDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedScope = RbReceipts.IsChecked == true ? CustomerExportScope.Receipts
            : RbSections.IsChecked == true ? CustomerExportScope.DeliverySections
            : RbPayments.IsChecked == true ? CustomerExportScope.Payments
            : CustomerExportScope.FullPage;
        DialogResult = true;
    }
}
