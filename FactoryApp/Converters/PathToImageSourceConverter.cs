using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace FactoryApp.Converters;

/// <summary>
/// Converts a local file path to BitmapImage for DataGrid thumbnail; returns null if missing or invalid.
/// </summary>
public class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            if (TryGetDecodePixelWidth(parameter, out var decodeW))
                bitmap.DecodePixelWidth = decodeW;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>Default 64px width for grid thumbnails; pass an integer (e.g. 400) for larger previews.</summary>
    private static bool TryGetDecodePixelWidth(object? parameter, out int width)
    {
        width = 64;
        if (parameter == null)
            return true;
        if (parameter is int i && i > 0)
        {
            width = i;
            return true;
        }
        if (parameter is string s && int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            width = parsed;
            return true;
        }
        return false;
    }
}
