using System.Globalization;
using System.Windows.Data;

namespace FactoryApp.Converters;

/// <summary>True when value is a number strictly less than zero (for stock shortage highlighting).</summary>
public sealed class IsNegativeDecimalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return false;
        try
        {
            var d = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return d < 0m;
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
