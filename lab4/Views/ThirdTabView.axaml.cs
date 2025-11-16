using System;
using System.Threading.Tasks;
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

    private async void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        var isBenchmarkTab = BenchmarkTab == (sender as TabControl)?.SelectedItem;
        if (isBenchmarkTab && _benchmarkViewModel.HasChart) {
            _benchmarkViewModel.RefreshChart(); // заставляем биндинг обновить Url
            // ReloadBenchmarkWebView();
        }

        if (_benchmarkStarted || !isBenchmarkTab) {
            return;
        }

        _benchmarkStarted = true;
        await StartBenchmarkAsync();
    }

    private Task StartBenchmarkAsync() {
        return _benchmarkViewModel.RunBenchmarkAsync();
    }

    private void ReloadBenchmarkWebView() {
        if (!_benchmarkViewModel.HasChart || string.IsNullOrWhiteSpace(_benchmarkViewModel.ChartUrl)) {
            return;
        }

        // var uri = new Uri(_benchmarkViewModel.ChartUrl);
        // Console.WriteLine("ReloadBenchmarkWebView: " + uri);
        // BenchmarkWebView.Url = uri;
        BenchmarkWebView.Reload();
    }

}