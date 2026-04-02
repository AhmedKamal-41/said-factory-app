using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Data.Sqlite;

namespace FactoryApp.Services;

/// <summary>Timestamped file copy of SQLite + optional multipart upload to Railway (POST, form fields: file, deviceId).</summary>
public sealed class BackupService
{
    private readonly FactoryAppOptions _options;
    private readonly DeviceIdService _deviceIdService;

    public BackupService(FactoryAppOptions options, DeviceIdService deviceIdService)
    {
        _options = options;
        _deviceIdService = deviceIdService;
    }

    private string AppRoot => _options.ResolveAppDataRoot();

    /// <summary>WAL checkpoint then copy live DB to a timestamped file under Backups\staging. Returns path or null if skipped/failed.</summary>
    public Task<string?> CreateBackupCopyAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = DatabaseInitializer.DatabaseFilePath;
            if (!File.Exists(sourcePath))
            {
                BackupLogger.Log(AppRoot, $"CreateBackupCopy: source database not found: {sourcePath}");
                return (string?)null;
            }

            var stagingDir = Path.Combine(AppRoot, "Backups", "staging");
            Directory.CreateDirectory(stagingDir);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var destPath = Path.Combine(stagingDir, $"factory_backup_{stamp}.db");

            try
            {
                // Flush WAL to main file for a consistent copy (short-lived connection; app uses discrete connections).
                using (var conn = new SqliteConnection(DatabaseInitializer.ConnectionString))
                {
                    conn.Open();
                    using var checkpoint = conn.CreateCommand();
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
                    try
                    {
                        checkpoint.ExecuteNonQuery();
                    }
                    catch (SqliteException)
                    {
                        // Not in WAL mode or checkpoint not applicable; copy may still be valid.
                    }
                }

                File.Copy(sourcePath, destPath, overwrite: true);
                BackupLogger.Log(AppRoot, $"CreateBackupCopy: created {destPath}");
                return destPath;
            }
            catch (Exception ex)
            {
                BackupLogger.Log(AppRoot, $"CreateBackupCopy failed: {ex.Message}");
                try
                {
                    if (File.Exists(destPath))
                        File.Delete(destPath);
                }
                catch
                {
                    // ignore
                }

                return null;
            }
        }, cancellationToken);
    }

    /// <summary>POST multipart/form-data: file + deviceId. Empty BackupApiUrl = skipped (returns true).</summary>
    public async Task<bool> UploadBackupAsync(string backupFilePath, string deviceId, CancellationToken cancellationToken = default)
    {
        var url = _options.BackupApiUrl?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            BackupLogger.Log(AppRoot, "UploadBackup: skipped (BackupApiUrl empty)");
            return true;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            await using var fileStream = new FileStream(
                backupFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(backupFilePath));
            content.Add(new StringContent(deviceId), "deviceId");

            using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                BackupLogger.Log(AppRoot, $"UploadBackup: success HTTP {(int)response.StatusCode}");
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var snippet = body.Length > 500 ? body[..500] + "…" : body;
            BackupLogger.Log(AppRoot, $"UploadBackup: failed HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {snippet}");
            return false;
        }
        catch (OperationCanceledException)
        {
            BackupLogger.Log(AppRoot, "UploadBackup: cancelled or timed out");
            return false;
        }
        catch (Exception ex)
        {
            BackupLogger.Log(AppRoot, $"UploadBackup: exception {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async Task RunBackupOnExitAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.BackupEnabled)
        {
            BackupLogger.Log(AppRoot, "RunBackupOnExit: skipped (BackupEnabled false)");
            return;
        }

        BackupLogger.Log(AppRoot, "RunBackupOnExit: started");
        var deviceId = _deviceIdService.EnsureDeviceId();

        string? backupPath;
        try
        {
            backupPath = await CreateBackupCopyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            BackupLogger.Log(AppRoot, "RunBackupOnExit: cancelled during copy");
            return;
        }

        if (string.IsNullOrEmpty(backupPath))
            return;

        var skipUpload = string.IsNullOrWhiteSpace(_options.BackupApiUrl?.Trim());
        if (skipUpload)
        {
            BackupLogger.Log(AppRoot, "RunBackupOnExit: BackupApiUrl empty; keeping local backup only (no upload)");
            try
            {
                var keepDir = Path.Combine(AppRoot, "Backups");
                Directory.CreateDirectory(keepDir);
                var dest = Path.Combine(keepDir, Path.GetFileName(backupPath));
                File.Move(backupPath, dest, overwrite: true);
                BackupLogger.Log(AppRoot, $"RunBackupOnExit: moved to {dest}");
            }
            catch (Exception ex)
            {
                BackupLogger.Log(AppRoot, $"RunBackupOnExit: could not move local backup: {ex.Message}");
            }

            return;
        }

        bool uploadOk;
        try
        {
            uploadOk = await UploadBackupAsync(backupPath, deviceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            BackupLogger.Log(AppRoot, "RunBackupOnExit: cancelled during upload");
            return;
        }

        try
        {
            if (uploadOk && _options.RetainLocalBackupOnSuccess)
            {
                var keepDir = Path.Combine(AppRoot, "Backups");
                Directory.CreateDirectory(keepDir);
                var name = Path.GetFileName(backupPath);
                var dest = Path.Combine(keepDir, name);
                if (!string.Equals(Path.GetFullPath(backupPath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(backupPath, dest, overwrite: true);
                    File.Delete(backupPath);
                }

                BackupLogger.Log(AppRoot, $"RunBackupOnExit: retained local copy at {dest}");
            }
            else if (uploadOk)
            {
                File.Delete(backupPath);
                BackupLogger.Log(AppRoot, "RunBackupOnExit: removed staging file after successful upload");
            }
            else
            {
                BackupLogger.Log(AppRoot, $"RunBackupOnExit: kept staging file for retry: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            BackupLogger.Log(AppRoot, $"RunBackupOnExit: cleanup error {ex.Message}");
        }
    }
}
