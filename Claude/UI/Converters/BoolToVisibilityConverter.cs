using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClaudeLauncher.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(
    object value,
    Type targetType,
    object parameter,
    CultureInfo culture)
    {
        return value is bool visible && visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}