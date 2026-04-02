using System.IO;

namespace FactoryApp.Services;

/// <summary>Persists a stable anonymous device id in device_id.txt under the app data root.</summary>
public sealed class DeviceIdService
{
    private static readonly object Gate = new();
    private readonly string _deviceIdPath;

    public DeviceIdService(FactoryAppOptions options)
    {
        var root = options.ResolveAppDataRoot();
        Directory.CreateDirectory(root);
        _deviceIdPath = Path.Combine(root, "device_id.txt");
    }

    /// <summary>Returns existing id or creates a new GUID on first run.</summary>
    public string EnsureDeviceId()
    {
        lock (Gate)
        {
            try
            {
                if (File.Exists(_deviceIdPath))
                {
                    var existing = File.ReadAllText(_deviceIdPath).Trim();
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }
            }
            catch
            {
                // fall through to regenerate
            }

            var id = Guid.NewGuid().ToString("D");
            try
            {
                var dir = Path.GetDirectoryName(_deviceIdPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_deviceIdPath, id);
            }
            catch
            {
                // still return id for this session even if persist failed
            }

            return id;
        }
    }
}
