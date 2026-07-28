using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeLauncher.UI.Helpers;

/// <summary>
/// Fixes a classic WPF gotcha that makes manual scrolling inside a page of
/// settings feel broken/"laggy": a <see cref="ComboBox"/> (closed or open)
/// under the mouse silently *consumes* the mouse wheel to change its own
/// selected item instead of letting the wheel event bubble up to the
/// enclosing <see cref="ScrollViewer"/>. On a settings page with several
/// stacked ComboBoxes (provider, model, …) this makes scrolling stop dead
/// (or, worse, silently change a dropdown's selection) the moment the
/// cursor passes over one of them while the user is trying to scroll past.
///
/// Attaching <c>PassthroughWheelToParent="True"</c> re-raises the wheel
/// event on the ComboBox's parent instead, so the page scrolls smoothly no
/// matter where the mouse happens to be, while an *open* dropdown still
/// scrolls its own item list normally (untouched).
/// </summary>
public static class ScrollWheelPassthroughBehavior
{
    public static readonly DependencyProperty PassthroughWheelToParentProperty =
        DependencyProperty.RegisterAttached(
            "PassthroughWheelToParent",
            typeof(bool),
            typeof(ScrollWheelPassthroughBehavior),
            new PropertyMetadata(false, OnPassthroughWheelToParentChanged));

    public static bool GetPassthroughWheelToParent(DependencyObject obj)
        => (bool)obj.GetValue(PassthroughWheelToParentProperty);

    public static void SetPassthroughWheelToParent(
        DependencyObject obj,
        bool value)
    {
        obj.SetValue(PassthroughWheelToParentProperty, value);
    }

    private static void OnPassthroughWheelToParentChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.PreviewMouseWheel -= OnPreviewMouseWheel;

        if ((bool)e.NewValue)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        // While the dropdown itself is open, let the wheel behave normally
        // (scrolling the open item list is expected there).
        if (sender is ComboBox { IsDropDownOpen: true })
            return;

        if (sender is not UIElement element)
            return;

        e.Handled = true;

        var parent = System.Windows.Media.VisualTreeHelper.GetParent(element) as UIElement;

        if (parent is null)
            return;

        var routedArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };

        parent.RaiseEvent(routedArgs);
    }
}
