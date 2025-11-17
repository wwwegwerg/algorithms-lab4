using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using lab4.Models;
using lab4.Services;

namespace lab4.ViewModels;

public class WordSortingViewModel : ViewModelBase {
    private string _inputText = string.Empty;
    private WordSortAlgorithm _selectedAlgorithm = WordSortAlgorithm.QuickSort;
    private string _statusMessage = "Вставьте текст и выберите алгоритм.";
    private string _sortedWordsPreview = string.Empty;
    private bool _hasResults;
    private TimeSpan _lastDuration = TimeSpan.Zero;
    private int _wordCount;

    public WordSortingViewModel() {
        AlgorithmOptions = new List<KeyValuePair<WordSortAlgorithm, string>> {
            new(WordSortAlgorithm.QuickSort, "Quick sort"),
            new(WordSortAlgorithm.RadixSort, "Radix sort")
        };
        WordFrequencies = new ObservableCollection<WordFrequency>();
    }

    public ObservableCollection<WordFrequency> WordFrequencies { get; }

    public IReadOnlyList<KeyValuePair<WordSortAlgorithm, string>> AlgorithmOptions { get; }

    public string InputText {
        get => _inputText;
        set => SetField(ref _inputText, value);
    }

    public WordSortAlgorithm SelectedAlgorithm {
        get => _selectedAlgorithm;
        set => SetField(ref _selectedAlgorithm, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool HasResults {
        get => _hasResults;
        private set => SetField(ref _hasResults, value);
    }

    public string SortedWordsPreview {
        get => _sortedWordsPreview;
        private set {
            if (SetField(ref _sortedWordsPreview, value)) {
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public bool HasPreview => !string.IsNullOrWhiteSpace(SortedWordsPreview);

    public string TotalWordsDisplay => HasResults ? _wordCount.ToString() : "-";

    public string DurationDisplay => HasResults ? $"{_lastDuration.TotalMilliseconds:0} мс" : "-";

    public void RunSorting() {
        var words = WordSortingService.ExtractWords(InputText);
        if (words.Count == 0) {
            ClearResults("Текст не содержит слов для сортировки.");
            return;
        }

        try {
            var stopwatch = Stopwatch.StartNew();
            var sorted = WordSortingService.SortWords(words, SelectedAlgorithm);
            stopwatch.Stop();
            var frequencies = WordSortingService.CountFrequencies(sorted);

            UpdateWordFrequencies(frequencies);
            SortedWordsPreview = string.Join(" ", sorted);
            _wordCount = sorted.Count;
            _lastDuration = stopwatch.Elapsed;
            HasResults = true;
            StatusMessage = $"Готово. Использован {GetAlgorithmLabel(SelectedAlgorithm)}.";
        } catch (Exception ex) {
            ClearResults($"Ошибка: {ex.Message}");
            return;
        }

        OnPropertyChanged(nameof(TotalWordsDisplay));
        OnPropertyChanged(nameof(DurationDisplay));
    }

    private void UpdateWordFrequencies(IEnumerable<WordFrequency> frequencies) {
        WordFrequencies.Clear();
        foreach (var frequency in frequencies) {
            WordFrequencies.Add(frequency);
        }
    }

    private string GetAlgorithmLabel(WordSortAlgorithm algorithm) {
        return AlgorithmOptions.First(option => option.Key == algorithm).Value;
    }

    private void ClearResults(string message) {
        WordFrequencies.Clear();
        SortedWordsPreview = string.Empty;
        HasResults = false;
        _wordCount = 0;
        _lastDuration = TimeSpan.Zero;
        StatusMessage = message;
        OnPropertyChanged(nameof(TotalWordsDisplay));
        OnPropertyChanged(nameof(DurationDisplay));
    }
}