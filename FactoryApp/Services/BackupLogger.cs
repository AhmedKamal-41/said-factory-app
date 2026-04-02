using System.IO;

namespace FactoryApp.Services;

/// <summary>Append-only backup log under %AppDataRoot%\Logs\backup.log</summary>
public static class BackupLogger
{
    private static readonly object Gate = new();

    public static void Log(string appDataRoot, string message)
    {
        try
        {
            var dir = Path.Combine(appDataRoot, "Logs");
            Directory.CreateDirectory(dir);
            var line = $"{DateTime.UtcNow:O}  {message}{Environment.NewLine}";
            var logPath = Path.Combine(dir, "backup.log");
            lock (Gate)
                File.AppendAllText(logPath, line);
        }
        catch
        {
            // Never throw from logging
        }
    }
}
