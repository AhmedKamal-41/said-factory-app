using System.Windows;
using FactoryApp.Services;

namespace FactoryApp;

public partial class TreasuryPasswordDialog : Window
{
    public TreasuryPasswordDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (TreasuryAccess.Verify(PasswordBox.Password))
        {
            DialogResult = true;
            return;
        }

        MessageBox.Show(this, "كلمة المرور غير صحيحة.", "الخزنة", MessageBoxButton.OK, MessageBoxImage.Warning);
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
