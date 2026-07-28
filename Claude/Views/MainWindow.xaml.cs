using ClaudeLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Collections.Specialized;

namespace ClaudeLauncher.Views;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel _viewModel;
    private double _lastAnimatedProgress;

    public MainWindow(
    LauncherViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        DataContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.LaunchLog.CollectionChanged += LaunchLog_CollectionChanged;

        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(
    object sender,
    RoutedEventArgs e)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        BeginAnimation(OpacityProperty, animation);
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.ActiveStepIndex))
        {
            ScrollActiveStepIntoView();
            return;
        }

        if (e.PropertyName != nameof(LauncherViewModel.Progress))
            return;

        // The ProgressBar's Value is also bound to Progress in XAML, which updates the
        // base value the instant the property changes — before this handler runs. If we
        // don't pass an explicit "From", BeginAnimation reads that already-updated base
        // value as the start point, so From == To and the bar snaps instead of filling.
        // Track the last value we animated to/from ourselves so the fill is always visible.
        var startValue = _lastAnimatedProgress;
        var targetValue = _viewModel.Progress;

        _lastAnimatedProgress = targetValue;

        if (Math.Abs(targetValue - startValue) < 0.01)
            return;

        var animation = new DoubleAnimation
        {
            From = startValue,
            To = targetValue,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            },
            FillBehavior = FillBehavior.HoldEnd
        };

        ProgressBar.BeginAnimation(
            System.Windows.Controls.ProgressBar.ValueProperty,
            animation);
    }

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var settingsWindow =
            App.Host.Services.GetRequiredService<SettingsWindow>();

        settingsWindow.Owner = this;

        settingsWindow.ShowDialog();

        // Settings writes config.json directly; the launcher screen edits and
        // persists the same file, so it has to pick up those changes instead
        // of overwriting them from its now-stale in-memory copy.
        _viewModel.ReloadConfiguration();
    }

    private void MoreMenuButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = MoreMenuButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        var relaunchItem = new MenuItem
        {
            Header = "Relaunch",
            Command = _viewModel.RelaunchCommand
        };

        var separator = new Separator();

        var exitItem = new MenuItem
        {
            Header = "Exit",
            Command = _viewModel.ExitCommand
        };

        menu.Items.Add(relaunchItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        menu.IsOpen = true;
    }

    /// <summary>
    /// Keeps the current (or failed) checklist row visible. Without this the
    /// last steps of a long checklist — "Launch Claude", "Ready" — sat below
    /// the visible area of the Activity card and looked cut off.
    /// </summary>
    private void ScrollActiveStepIntoView()
    {
        var index = _viewModel.ActiveStepIndex;

        if (index < 0)
        {
            StepsViewer.ScrollToTop();
            return;
        }

        // Containers are generated during layout, so ask again once WPF is
        // done with this pass — otherwise ContainerFromIndex returns null for
        // the very first steps.
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (index >= StepsList.Items.Count)
                    return;

                if (StepsList.ItemContainerGenerator.ContainerFromIndex(index)
                    is FrameworkElement container)
                {
                    container.BringIntoView();
                }
            }));
    }

    private void LaunchLog_CollectionChanged(
    object? sender,
    NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogsViewer.ScrollToEnd();
        });
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.LaunchLog.CollectionChanged -= LaunchLog_CollectionChanged;
    }
}