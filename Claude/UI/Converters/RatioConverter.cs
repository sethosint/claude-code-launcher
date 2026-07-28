using System.Globalization;
using System.Windows.Data;

namespace ClaudeLauncher.UI.Converters;

/// <summary>
/// Multiplies a size (typically an ActualWidth/ActualHeight) by the ratio
/// passed as ConverterParameter.
///
/// Used by the Activity card: the step checklist is auto-sized so it takes
/// exactly the room its rows need (and the log area gets everything that is
/// left, instead of a fixed proportional slice that leaves a dead gap), but
/// it is capped at a fraction of the card so a small window can never let
/// the checklist push the log — or the card's own bottom edge — out of view.
/// Above the cap the checklist scrolls internally instead of being clipped.
/// </summary>
public sealed class RatioConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not double size || double.IsNaN(size) || double.IsInfinity(size))
            return double.PositiveInfinity;

        var ratio = 1.0;
        var offset = 0.0;

        // Parameter is either "0.62" (plain ratio) or "0.66;42"
        // (ratio applied after subtracting a fixed offset — e.g. the card's
        // own header and spacer rows, which are not available to the
        // checklist in the first place).
        if (parameter is string text)
        {
            var parts = text.Split(';', StringSplitOptions.TrimEntries);

            if (parts.Length > 0 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRatio))
            {
                ratio = parsedRatio;
            }

            if (parts.Length > 1 &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedOffset))
            {
                offset = parsedOffset;
            }
        }
        else if (parameter is double direct)
        {
            ratio = direct;
        }

        var result = (size - offset) * ratio;

        // A zero/negative cap would collapse the checklist entirely during
        // the very first layout pass (before the card has a size yet).
        return result > 0 ? result : double.PositiveInfinity;
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
