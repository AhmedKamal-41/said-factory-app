using System.IO;
using System.Windows;
using FactoryApp.Services;

namespace FactoryApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppConfigurationLoader.Load();
            ThemeManager.Initialize();
            base.OnStartup(e);

            _ = new DeviceIdService(AppConfigurationLoader.Options).EnsureDeviceId();

            DatabaseInitializer.Initialize();

            // While the login dialog is open it is the only window; when it closes with OK, there is a
            // brief moment with no windows. OnLastWindowClose would shut down the app before MainWindow shows.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var login = new LoginWindow();
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var opts = AppConfigurationLoader.Options;
                var backup = new BackupService(opts, new DeviceIdService(opts));
                Task.Run(() => backup.RunBackupOnExitAsync(cts.Token)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try
                {
                    var root = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FactoryApp");
                    BackupLogger.Log(root, "OnExit backup: " + ex.Message);
                }
                catch
                {
                    // ignore
                }
            }

            base.OnExit(e);
        }
    }
}
