using System.IO;
using Microsoft.Extensions.Configuration;

namespace FactoryApp.Services;

/// <summary>Loads appsettings.json from <see cref="AppContext.BaseDirectory"/> (next to the EXE when published).</summary>
public static class AppConfigurationLoader
{
    public static FactoryAppOptions Options { get; private set; } = new();

    public static void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            Options = new FactoryAppOptions();
            DatabaseInitializer.SetDatabaseFilePath(Options.EffectiveDatabasePath);
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .Build();

        var bound = new FactoryAppOptions();
        configuration.GetSection("FactoryApp").Bind(bound);
        Options = bound;
        DatabaseInitializer.SetDatabaseFilePath(Options.EffectiveDatabasePath);
    }
}
