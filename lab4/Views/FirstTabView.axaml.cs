using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using lab4.ViewModels;

namespace lab4.Views;

public partial class FirstTabView : UserControl {
    private bool _autoScrollEnabled = true;
    private bool _suppressAutoScrollToggleHandler;

    private SortingVisualizerViewModel ViewModel => (SortingVisualizerViewModel)DataContext!;

    public FirstTabView() {
        InitializeComponent();
        DataContext = new SortingVisualizerViewModel();
        SubscribeToLogUpdates();
        SetupLogInteractionHandlers();
        InitializeAutoScrollToggle();
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e) => ViewModel.TogglePlayPause();

    private void OnStepClick(object? sender, RoutedEventArgs e) => ViewModel.Step();

    private void OnRandomizeClick(object? sender, RoutedEventArgs e) => ViewModel.GenerateRandomArray();

    private void OnApplyArrayClick(object? sender, RoutedEventArgs e) => ViewModel.ApplyManualArray();

    private void SubscribeToLogUpdates() {
        ViewModel.LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;
    }

    private void SetupLogInteractionHandlers() {
        if (LogListBox == null) {
            return;
        }

        LogListBox.PointerWheelChanged += OnLogPointerWheelChanged;
        LogListBox.PointerPressed += OnLogPointerPressed;
    }

    private void InitializeAutoScrollToggle() {
        if (AutoScrollToggle == null) {
            return;
        }

        _suppressAutoScrollToggleHandler = true;
        AutoScrollToggle.IsChecked = true;
        _suppressAutoScrollToggleHandler = false;
        _autoScrollEnabled = true;
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action != NotifyCollectionChangedAction.Add || LogListBox == null || !_autoScrollEnabled) {
            return;
        }

        Dispatcher.UIThread.Post(ScrollLogToEnd);
    }

    private void ScrollLogToEnd() {
        if (LogListBox?.Items == null) {
            return;
        }

        var lastItem = LogListBox.Items.Cast<object>().LastOrDefault();
        if (lastItem != null) {
            LogListBox.ScrollIntoView(lastItem);
        }
    }

    private void OnLogPointerWheelChanged(object? sender, PointerWheelEventArgs e) => PauseAutoScroll();

    private void OnLogPointerPressed(object? sender, PointerPressedEventArgs e) => PauseAutoScroll();

    private void PauseAutoScroll() {
        if (!_autoScrollEnabled) {
            return;
        }

        _autoScrollEnabled = false;
        UpdateAutoScrollToggle(false);
    }

    private void UpdateAutoScrollToggle(bool isChecked) {
        if (AutoScrollToggle == null) {
            return;
        }

        _suppressAutoScrollToggleHandler = true;
        AutoScrollToggle.IsChecked = isChecked;
        _suppressAutoScrollToggleHandler = false;
    }

    private void OnAutoScrollToggleChecked(object? sender, RoutedEventArgs e) {
        if (_suppressAutoScrollToggleHandler) {
            return;
        }

        _autoScrollEnabled = true;
        ScrollLogToEnd();
    }

    private void OnAutoScrollToggleUnchecked(object? sender, RoutedEventArgs e) {
        if (_suppressAutoScrollToggleHandler) {
            return;
        }

        _autoScrollEnabled = false;
    }
}
