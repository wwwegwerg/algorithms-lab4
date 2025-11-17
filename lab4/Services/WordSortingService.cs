using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using lab4.Models;

namespace lab4.Services;

public static class WordSortingService {
    private static readonly Regex WordRegex = new(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled);

    public static List<string> ExtractWords(string? source) {
        if (string.IsNullOrWhiteSpace(source)) {
            return new List<string>();
        }

        return WordRegex
            .Matches(source)
            .Select(match => NormalizeWord(match.Value))
            .Where(word => word.Length > 0)
            .ToList();
    }

    public static List<string> SortWords(IEnumerable<string>? source, WordSortAlgorithm algorithm) {
        var data = source?.Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(NormalizeWord)
            .ToList() ?? new List<string>();

        if (data.Count <= 1) {
            return data;
        }

        switch (algorithm) {
            case WordSortAlgorithm.QuickSort:
                QuickSort(data, 0, data.Count - 1, RussianStringComparer.Instance);
                break;
            case WordSortAlgorithm.RadixSort:
                RadixSort(data);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null);
        }

        return data;
    }

    private static string NormalizeWord(string word) {
        if (string.IsNullOrWhiteSpace(word)) {
            return string.Empty;
        }

        var normalized = word.Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Приводим латинскую ë и возможные комбинированные варианты к кириллической ё
        normalized = normalized.Replace('\u00eb', 'ё'); // латинская ë
        normalized = normalized.Replace("е\u0308", "ё"); // кириллическая е + комб. точки
        normalized = normalized.Replace("ё\u0308", "ё"); // редкие сочетания

        return normalized;
    }

    public static IReadOnlyList<WordFrequency> CountFrequencies(IList<string> sortedWords) {
        var result = new List<WordFrequency>();
        if (sortedWords.Count == 0) {
            return result;
        }

        var currentWord = sortedWords[0];
        var currentCount = 1;
        for (var i = 1; i < sortedWords.Count; i++) {
            if (sortedWords[i] == currentWord) {
                currentCount++;
                continue;
            }

            result.Add(new WordFrequency(currentWord, currentCount));
            currentWord = sortedWords[i];
            currentCount = 1;
        }

        result.Add(new WordFrequency(currentWord, currentCount));
        return result;
    }

    private static void QuickSort(List<string> items, int low, int high, IComparer<string> comparer) {
        if (low >= high) {
            return;
        }

        var pivotIndex = Partition(items, low, high, comparer);
        QuickSort(items, low, pivotIndex - 1, comparer);
        QuickSort(items, pivotIndex + 1, high, comparer);
    }

    private static int Partition(IList<string> items, int low, int high, IComparer<string> comparer) {
        var pivotValue = items[high];
        var smallerIndex = low - 1;

        for (var j = low; j < high; j++) {
            if (comparer.Compare(items[j], pivotValue) > 0) {
                continue;
            }

            smallerIndex++;
            if (smallerIndex == j) {
                continue;
            }

            (items[smallerIndex], items[j]) = (items[j], items[smallerIndex]);
        }

        if (smallerIndex + 1 != high) {
            (items[smallerIndex + 1], items[high]) = (items[high], items[smallerIndex + 1]);
        }

        return smallerIndex + 1;
    }

    private static void RadixSort(List<string> items) {
        if (items.Count <= 1) {
            return;
        }

        var maxLength = items.Max(word => word.Length);
        if (maxLength == 0) {
            return;
        }

        var source = items.ToArray();
        var destination = new string[source.Length];

        for (var position = maxLength - 1; position >= 0; position--) {
            var maxOrder = 0;
            foreach (var word in source) {
                maxOrder = Math.Max(maxOrder, GetOrderValue(word, position));
            }

            var counts = new int[maxOrder + 2];

            foreach (var word in source) {
                counts[GetOrderValue(word, position)]++;
            }

            var sum = 0;
            for (var i = 0; i < counts.Length; i++) {
                var temp = counts[i];
                counts[i] = sum;
                sum += temp;
            }

            foreach (var word in source) {
                var bucket = GetOrderValue(word, position);
                destination[counts[bucket]++] = word;
            }

            (source, destination) = (destination, source);
        }

        items.Clear();
        items.AddRange(source);
    }

    private static int GetOrderValue(string word, int position) {
        if (position >= word.Length || position < 0) {
            return 0;
        }

        return RussianStringComparer.GetOrderValue(word[position]);
    }

    private sealed class RussianStringComparer : IComparer<string> {
        private static readonly Dictionary<char, int> AlphabetOrder = BuildAlphabetOrder();
        private const int FallbackOffset = 1000;

        public static RussianStringComparer Instance { get; } = new();

        public int Compare(string? x, string? y) {
            if (ReferenceEquals(x, y)) {
                return 0;
            }

            if (x is null) {
                return -1;
            }

            if (y is null) {
                return 1;
            }

            var minLength = Math.Min(x.Length, y.Length);
            for (var i = 0; i < minLength; i++) {
                var leftOrder = GetOrderValue(x[i]);
                var rightOrder = GetOrderValue(y[i]);
                if (leftOrder == rightOrder) {
                    continue;
                }

                return leftOrder.CompareTo(rightOrder);
            }

            return x.Length.CompareTo(y.Length);
        }

        internal static int GetOrderValue(char character) {
            var lower = char.ToLowerInvariant(character);
            if (AlphabetOrder.TryGetValue(lower, out var value)) {
                return value;
            }

            return FallbackOffset + lower;
        }

        private static Dictionary<char, int> BuildAlphabetOrder() {
            const string alphabet = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            var order = new Dictionary<char, int>(alphabet.Length);
            for (var i = 0; i < alphabet.Length; i++) {
                order[alphabet[i]] = i + 1; // начинаем с 1, 0 оставляем для "пустого" символа
            }

            return order;
        }
    }
}
