using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using lab4.Charts;
using lab4.Models;
using lab4.Services;

namespace lab4.ViewModels;

public class WordSortBenchmarkViewModel : ViewModelBase {
    // private static readonly int[] SampleSizes = [100, 500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000];
    private static readonly int[] SampleSizes = [100, 500, 1_000, 2_000, 5_000];
    private string _statusMessage = "Здесь появится сравнение Quick sort и Radix sort.";
    private bool _isRunning;

    public string StatusMessage {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? ChartFilePath { get; private set; }

    public bool HasChart => !string.IsNullOrWhiteSpace(ChartFilePath) && ChartFilePath.Length > 0;

    private bool IsRunning {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public async Task RunBenchmarkAsync() {
        if (IsRunning) {
            return;
        }

        try {
            IsRunning = true;
            StatusMessage = "Чтение входных данных...";
            var filePath = Path.Combine(AppContext.BaseDirectory, "task3.input", "words.txt");
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("Файл со словами не найден.", filePath);
            }

            var fileText = await File.ReadAllTextAsync(filePath);
            var allWords = WordSortingService.ExtractWords(fileText);
            if (allWords.Count < SampleSizes.Max()) {
                throw new InvalidOperationException(
                    $"В файле найдено всего {allWords.Count} слов — нужно минимум {SampleSizes.Max()}.");
            }

            StatusMessage = "Выполняем замеры...";

            ChartFilePath = await Task.Run(() => BuildChart(allWords));
            StatusMessage = "Готово.";
        } catch (Exception ex) {
            StatusMessage = $"Ошибка: {ex.Message}";
        } finally {
            IsRunning = false;
        }
    }

    private static string BuildChart(IReadOnlyList<string> allWords) {
        var quickSortPoints = new List<DataPoint>();
        var radixSortPoints = new List<DataPoint>();
        var sw = Stopwatch.StartNew();

        foreach (var size in SampleSizes) {
            var sample = allWords.Take(size).ToList();
            var quickMs = MeasureSort(sample, WordSortAlgorithm.QuickSort);
            var radixMs = MeasureSort(sample, WordSortAlgorithm.RadixSort);

            quickSortPoints.Add(new DataPoint(size, quickMs));
            radixSortPoints.Add(new DataPoint(size, radixMs));
        }

        sw.Stop();
        var chartData = new ChartData(
            "Сравнение времени сортировки слов",
            new List<(string, IList<DataPoint>)> {
                ("Quick sort", quickSortPoints),
                ("Radix sort", radixSortPoints)
            },
            "Количество слов",
            "Время, мс",
            sw.Elapsed.TotalSeconds
        );

        var chartPath = ChartBuilder.Build2DLineChart(chartData, promptOnOverwrite: false);
        return chartPath;
    }

    private static double MeasureSort(IReadOnlyList<string> source, WordSortAlgorithm algorithm) {
        Benchmark.Warmup(SortAction, warmupCount: 1);
        return Benchmark.MeasureDurationInMs(SortAction, repetitionCount: 3);

        void SortAction() {
            var copy = new List<string>(source);
            WordSortingService.SortWords(copy, algorithm);
        }
    }
}