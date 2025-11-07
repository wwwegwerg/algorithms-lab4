using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace lab4;

public partial class MainWindow : Window
{
    private const double SquareSize = 60;
    private const double SquareMargin = 8;
    private const double TopOffset = 10;

    public MainWindow()
    {
        InitializeComponent();

        SquaresScrollViewer.SizeChanged += (_, _) => UpdateSquares();

        UpdateSquares();
    }

    private void InputBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSquares();
    }

    private void UpdateSquares()
    {
        if (SquaresCanvas is null || SquaresScrollViewer is null)
            return;

        SquaresCanvas.Children.Clear();

        var text = InputBox.Text ?? string.Empty;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        double viewportWidth = SquaresScrollViewer.Viewport.Width;

        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
            viewportWidth = SquaresScrollViewer.Bounds.Width;

        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
            viewportWidth = 400;

        double maxWidth = Math.Max(viewportWidth, SquareSize);

        double x = 0;
        double y = TopOffset;
        double rowHeight = SquareSize;

        foreach (var part in parts)
        {
            if (!double.TryParse(part,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var value) &&
                !double.TryParse(part,
                    NumberStyles.Any,
                    CultureInfo.CurrentCulture,
                    out value))
            {
                continue;
            }

            int intValue = (int)Math.Floor(value);

            if (x + SquareSize > maxWidth)
            {
                x = 0;
                y += rowHeight + SquareMargin;
            }

            var border = new Border
            {
                Width = SquareSize,
                Height = SquareSize,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Background = Brushes.LightGray,
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = intValue.ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);

            SquaresCanvas.Children.Add(border);

            x += SquareSize + SquareMargin;
        }

        double usedHeight = y + rowHeight + TopOffset;
        SquaresCanvas.Height = Math.Max(usedHeight, SquaresCanvas.MinHeight);
        SquaresCanvas.Width = maxWidth;
    }
}