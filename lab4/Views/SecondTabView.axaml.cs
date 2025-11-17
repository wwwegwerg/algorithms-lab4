using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using lab4.ViewModels;

namespace lab4.Views;

public partial class SecondTabView : UserControl {
    private bool _autoScrollEnabled = true;
    private bool _suppressAutoScrollToggleHandler;

    private ExternalSortingViewModel ViewModel => (ExternalSortingViewModel)DataContext!;

    public SecondTabView() {
        InitializeComponent();
        DataContext = new ExternalSortingViewModel();
        SubscribeToLogUpdates();
        SetupLogInteractionHandlers();
        InitializeAutoScrollToggle();
    }

    private async void OnLoadCsvClick(object? sender, RoutedEventArgs e) {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) {
            return;
        }

        var options = new FilePickerOpenOptions {
            Title = "Выберите CSV-файл",
            AllowMultiple = false,
            FileTypeFilter = [
                new FilePickerFileType("CSV файлы") { Patterns = ["*.csv"] },
                FilePickerFileTypes.All
            ]
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        var file = files.FirstOrDefault();
        if (file == null) {
            return;
        }

        var path = await EnsureLocalPathAsync(file);
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        ViewModel.LoadFromFile(path);
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e) => ViewModel.TogglePlayPause();

    private void OnStepClick(object? sender, RoutedEventArgs e) => ViewModel.Step();

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

    private static async Task<string?> EnsureLocalPathAsync(IStorageFile file) {
        var uriPath = file.Path?.LocalPath;
        if (!string.IsNullOrWhiteSpace(uriPath)) {
            return Uri.UnescapeDataString(uriPath);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"csv_{Guid.NewGuid():N}_{file.Name}");
        await using var sourceStream = await file.OpenReadAsync();
        await using var destinationStream = File.Create(tempPath);
        await sourceStream.CopyToAsync(destinationStream);
        return tempPath;
    }
}