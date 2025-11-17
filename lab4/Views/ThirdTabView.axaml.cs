using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using lab4.ViewModels;

namespace lab4.Views;

public partial class ThirdTabView : UserControl {
    private readonly WordSortingViewModel _sortingViewModel = new();
    private readonly WordSortBenchmarkViewModel _benchmarkViewModel = new();
    private bool _benchmarkStarted;
    private AvaloniaWebView.WebView? _benchmarkWebView;

    public ThirdTabView() {
        InitializeComponent();
        DataContext = _sortingViewModel;
        BenchmarkRoot.DataContext = _benchmarkViewModel;
        CreateBenchmarkWebView();
    }

    private void OnSortClick(object? sender, RoutedEventArgs e) {
        _sortingViewModel.RunSorting();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        CreateBenchmarkWebView();
        RefreshBenchmarkWebView();
    }

    private void OnBenchmarkWebViewAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) {
        RefreshBenchmarkWebView();
    }

    private async void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        var isBenchmarkTab = BenchmarkTab == (sender as TabControl)?.SelectedItem;
        if (isBenchmarkTab) {
            CreateBenchmarkWebView();
            RefreshBenchmarkWebView();
        } else {
            DestroyBenchmarkWebView();
        }

        if (_benchmarkStarted || !isBenchmarkTab) {
            return;
        }

        _benchmarkStarted = true;
        await _benchmarkViewModel.RunBenchmarkAsync();
        var url = _benchmarkViewModel.ChartFilePath;
        if (!string.IsNullOrWhiteSpace(url)) {
            _benchmarkWebView!.Url = new Uri(url);
        }
    }

    private void RefreshBenchmarkWebView() {
        if (!_benchmarkViewModel.HasChart || !BenchmarkTab.IsSelected || _benchmarkWebView is null) {
            return;
        }

        var url = _benchmarkViewModel.ChartFilePath;
        if (string.IsNullOrWhiteSpace(url)) {
            return;
        }

        try {
            _benchmarkWebView.Url = null; // force navigation to reinitialize native view after tab reattach
            _benchmarkWebView.Url = new Uri(url);
            _benchmarkWebView.Reload();
        } catch {
            // если native слой WebView умер, пересоздадим и попробуем снова
            CreateBenchmarkWebView();
            _benchmarkWebView!.Url = new Uri(url);
            _benchmarkWebView.Reload();
        }
    }

    private void OnBenchmarkWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == IsVisibleProperty && _benchmarkWebView is { IsVisible: true }) {
            // TabControl toggles visibility when switching tabs; re-navigate to wake WebView back up.
            RefreshBenchmarkWebView();
        }
    }

    private void CreateBenchmarkWebView() {
        if (_benchmarkWebView != null) {
            _benchmarkWebView.AttachedToVisualTree -= OnBenchmarkWebViewAttachedToVisualTree;
            _benchmarkWebView.PropertyChanged -= OnBenchmarkWebViewPropertyChanged;
        }

        BenchmarkWebViewHost.Children.Clear();

        _benchmarkWebView = new AvaloniaWebView.WebView {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _benchmarkWebView.AttachedToVisualTree += OnBenchmarkWebViewAttachedToVisualTree;
        _benchmarkWebView.PropertyChanged += OnBenchmarkWebViewPropertyChanged;
        BenchmarkWebViewHost.Children.Add(_benchmarkWebView);
    }

    private void DestroyBenchmarkWebView() {
        if (_benchmarkWebView == null) {
            return;
        }

        _benchmarkWebView.AttachedToVisualTree -= OnBenchmarkWebViewAttachedToVisualTree;
        _benchmarkWebView.PropertyChanged -= OnBenchmarkWebViewPropertyChanged;
        BenchmarkWebViewHost.Children.Clear();
        _benchmarkWebView = null;
    }
}