using System;
using System.Windows;
using System.Windows.Controls;

namespace FactoryApp
{
    public partial class ThemeToggleControl : UserControl
    {
        public ThemeToggleControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Refresh();

        private void OnClick(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
            Refresh();
        }

        private void Refresh()
        {
            if (ThemeManager.IsDark)
            {
                MoonPath.Visibility = Visibility.Collapsed;
                SunPath.Visibility = Visibility.Visible;
                LabelText.Text = "فاتح";
                RootButton.ToolTip = "التبديل إلى الوضع الفاتح";
            }
            else
            {
                MoonPath.Visibility = Visibility.Visible;
                SunPath.Visibility = Visibility.Collapsed;
                LabelText.Text = "داكن";
                RootButton.ToolTip = "التبديل إلى الوضع الداكن";
            }
        }
    }
}
