using System.IO;

namespace FactoryApp.Services;

/// <summary>Bound from appsettings.json section "FactoryApp". Railway: set <see cref="BackupApiUrl"/> to full POST URL (e.g. https://your-app.up.railway.app/upload-backup). Leave empty to skip cloud upload.</summary>
public sealed class FactoryAppOptions
{
    /// <summary>Override root for device id, logs, and backup folders. Empty = %LocalAppData%\FactoryApp</summary>
    public string? AppDataRoot { get; set; }

    /// <summary>Full path to the live SQLite database. Empty = {resolved AppDataRoot}\factory.db</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Full URL for POST multipart backup (include path, e.g. .../upload-backup). Empty = upload skipped.</summary>
    public string? BackupApiUrl { get; set; }

    public bool BackupEnabled { get; set; } = true;

    /// <summary>If true, after a successful upload the timestamped file is kept under Backups\; otherwise the staging copy is deleted.</summary>
    public bool RetainLocalBackupOnSuccess { get; set; }

    public string ResolveAppDataRoot()
    {
        if (!string.IsNullOrWhiteSpace(AppDataRoot))
            return Path.GetFullPath(AppDataRoot.Trim());
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactoryApp");
    }

    public string EffectiveDatabasePath =>
        string.IsNullOrWhiteSpace(DatabasePath)
            ? Path.Combine(ResolveAppDataRoot(), "factory.db")
            : Path.GetFullPath(DatabasePath.Trim());
}
