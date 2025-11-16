using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using lab4.ViewModels;

namespace lab4.Views;

public partial class ThirdTabView : UserControl {
    private readonly WordSortingViewModel _sortingViewModel = new();
    private readonly WordSortBenchmarkViewModel _benchmarkViewModel = new();
    private bool _benchmarkStarted;

    public ThirdTabView() {
        InitializeComponent();
        DataContext = _sortingViewModel;
        BenchmarkRoot.DataContext = _benchmarkViewModel;
    }

    private void OnSortClick(object? sender, RoutedEventArgs e) {
        _sortingViewModel.RunSorting();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        RefreshBenchmarkWebView();
    }

    private async void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        var isBenchmarkTab = BenchmarkTab == (sender as TabControl)?.SelectedItem;
        if (isBenchmarkTab) {
            RefreshBenchmarkWebView();
        }

        if (_benchmarkStarted || !isBenchmarkTab) {
            return;
        }

        _benchmarkStarted = true;
        var q = await _benchmarkViewModel.RunBenchmarkAsync();
        if (!string.IsNullOrWhiteSpace(q)) {
            BenchmarkWebView.Url = new Uri(q);
        }
    }

    private void RefreshBenchmarkWebView() {
        if (!_benchmarkViewModel.HasChart || !BenchmarkTab.IsSelected) {
            return;
        }

        var url = _benchmarkViewModel.ChartFilePath;
        if (string.IsNullOrWhiteSpace(url)) {
            return;
        }

        BenchmarkWebView.Url = new Uri(url);
        BenchmarkWebView.Reload();
    }
}