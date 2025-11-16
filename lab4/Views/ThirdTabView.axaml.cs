using Avalonia.Controls;
using Avalonia.Interactivity;
using lab4.ViewModels;

namespace lab4.Views;

public partial class ThirdTabView : UserControl {
    public ThirdTabView() {
        InitializeComponent();
        DataContext = new WordSortingViewModel();
    }

    private void OnSortClick(object? sender, RoutedEventArgs e) {
        if (DataContext is WordSortingViewModel viewModel) {
            viewModel.RunSorting();
        }
    }
}
