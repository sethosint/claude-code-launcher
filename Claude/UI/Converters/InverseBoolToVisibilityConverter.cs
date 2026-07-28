using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClaudeLauncher.UI.Converters;

/// <summary>
/// Opposite of <see cref="BoolToVisibilityConverter"/>: true collapses, false shows.
/// Used to toggle between two overlaid controls (e.g. a masked PasswordBox and a
/// plain-text TextBox) that share the same bound value.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is bool visible && visible
            ? Visibility.Collapsed
            : Visibility.Visible;
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
