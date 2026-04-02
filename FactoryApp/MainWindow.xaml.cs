using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace FactoryApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                VersionFooterText.Text = v != null
                    ? $"نظام إدارة المصنع — الإصدار {v.Major}.{v.Minor}.{v.Build}"
                    : "نظام إدارة المصنع";
            };
        }

        public void ShowDashboard()
        {
            DashboardHost.Visibility = Visibility.Visible;
            ModuleHost.Visibility = Visibility.Collapsed;
            ModuleHost.Content = null;
        }

        private void NavigateTo(UserControl module)
        {
            DashboardHost.Visibility = Visibility.Collapsed;
            ModuleHost.Visibility = Visibility.Visible;
            ModuleHost.Content = module;
        }

        private void OpenSupplier_Click(object sender, RoutedEventArgs e) =>
            NavigateTo(new SupplierWindow(this));

        private void OpenFactory_Click(object sender, RoutedEventArgs e) =>
            NavigateTo(new FactoryWindow(this));

        private void OpenTreasury_Click(object sender, RoutedEventArgs e)
        {
            var gate = new TreasuryPasswordDialog(this);
            if (gate.ShowDialog() != true)
                return;

            NavigateTo(new TreasuryWindow(this));
        }

        private void OpenCustomer_Click(object sender, RoutedEventArgs e) =>
            NavigateTo(new CustomerWindow(this));

        private void OpenInventory_Click(object sender, RoutedEventArgs e) =>
            NavigateTo(new InventoryWindow(this));

        private void OpenDdidEntry_Click(object sender, RoutedEventArgs e) =>
            NavigateTo(new DDIDEntryWindow(this));
    }
}
