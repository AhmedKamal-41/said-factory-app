using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FactoryApp.Services;

namespace FactoryApp;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static string GetCurrentPassword(PasswordBox passwordBox, TextBox plainTextBox) =>
        plainTextBox.Visibility == Visibility.Visible ? plainTextBox.Text : passwordBox.Password;

    private static void ApplyPasswordReveal(bool reveal, PasswordBox passwordBox, TextBox plainTextBox)
    {
        if (reveal)
        {
            plainTextBox.Text = passwordBox.Password;
            passwordBox.Visibility = Visibility.Collapsed;
            plainTextBox.Visibility = Visibility.Visible;
            plainTextBox.Focus();
            plainTextBox.CaretIndex = plainTextBox.Text.Length;
        }
        else
        {
            passwordBox.Password = plainTextBox.Text;
            plainTextBox.Visibility = Visibility.Collapsed;
            passwordBox.Visibility = Visibility.Visible;
            passwordBox.Focus();
        }
    }

    private void PasswordReveal_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb)
            return;

        var reveal = tb.IsChecked == true;
        if (tb == NewPasswordRevealToggle)
            ApplyPasswordReveal(reveal, NewPasswordBox, NewPasswordTextBox);
        else if (tb == ConfirmPasswordRevealToggle)
            ApplyPasswordReveal(reveal, ConfirmPasswordBox, ConfirmPasswordTextBox);
        else if (tb == LoginPasswordRevealToggle)
            ApplyPasswordReveal(reveal, LoginPasswordBox, LoginPasswordTextBox);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AppPasswordService.HasPassword())
        {
            Title = "تسجيل الدخول";
            LoginPanel.Visibility = Visibility.Visible;
            FirstRunPanel.Visibility = Visibility.Collapsed;
            LoginPasswordBox.Focus();
        }
        else
        {
            Title = "إعداد كلمة المرور";
            FirstRunPanel.Visibility = Visibility.Visible;
            LoginPanel.Visibility = Visibility.Collapsed;
            NewPasswordBox.Focus();
        }
    }

    private void FirstRunOk_Click(object sender, RoutedEventArgs e)
    {
        var newPwd = GetCurrentPassword(NewPasswordBox, NewPasswordTextBox);
        var confirmPwd = GetCurrentPassword(ConfirmPasswordBox, ConfirmPasswordTextBox);

        if (newPwd != confirmPwd)
        {
            MessageBox.Show(this, "كلمتا المرور غير متطابقتين.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (newPwd.Length < 4)
        {
            MessageBox.Show(this, "كلمة المرور يجب ألا تقل عن 4 أحرف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppPasswordService.SetPassword(newPwd);
        DialogResult = true;
    }

    private void LoginOk_Click(object sender, RoutedEventArgs e)
    {
        var pwd = GetCurrentPassword(LoginPasswordBox, LoginPasswordTextBox);
        if (!AppPasswordService.Verify(pwd))
        {
            MessageBox.Show(this, "كلمة المرور غير صحيحة.", "تسجيل الدخول", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoginPasswordTextBox.Clear();
            LoginPasswordBox.Clear();
            LoginPasswordRevealToggle.IsChecked = false;
            LoginPasswordTextBox.Visibility = Visibility.Collapsed;
            LoginPasswordBox.Visibility = Visibility.Visible;
            LoginPasswordBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
