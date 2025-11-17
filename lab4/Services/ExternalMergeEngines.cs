using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using lab4.Models;

namespace lab4.Services;

public static class ExternalMergeEngines {
    public static IReadOnlyList<ExternalSortAction> BuildActions(
        IReadOnlyList<CsvRowData> rows,
        ExternalMergeAlgorithm algorithm,
        int keyColumnIndex,
        string columnLabel) {
        if (rows.Count == 0) {
            return [
                new ExternalSortAction(
                    ExternalSortActionType.Finished,
                    message: "Нет данных для сортировки")
            ];
        }

        var normalizedKeyIndex = Math.Max(0, keyColumnIndex);
        var keyName = string.IsNullOrWhiteSpace(columnLabel)
            ? $"Колонка {normalizedKeyIndex + 1}"
            : columnLabel;

        var lookup = rows.ToDictionary(r => r.Id);
        var order = rows.Select(r => r.Id).ToList();

        return algorithm switch {
            ExternalMergeAlgorithm.StraightMerge => BuildStraight(order, lookup, normalizedKeyIndex, keyName),
            ExternalMergeAlgorithm.NaturalMerge => BuildNatural(order, lookup, normalizedKeyIndex, keyName),
            ExternalMergeAlgorithm.MultiwayMerge => BuildMultiway(order, lookup, normalizedKeyIndex, keyName),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }

    private static IReadOnlyList<ExternalSortAction> BuildStraight(
        List<int> order,
        IReadOnlyDictionary<int, CsvRowData> lookup,
        int keyColumnIndex,
        string columnLabel) {
        var actions = new List<ExternalSortAction>();
        var n = order.Count;
        var runSize = 1;
        var passNumber = 1;

        while (runSize < n) {
            for (var start = 0; start < n; start += 2 * runSize) {
                var mid = Math.Min(start + runSize, n);
                var end = Math.Min(start + 2 * runSize, n);
                MergeRuns(order, lookup, start, mid, end, keyColumnIndex, columnLabel, actions);
            }

            actions.Add(new ExternalSortAction(
                ExternalSortActionType.PassComplete,
                message: $"Проход #{passNumber}: серии длиной {runSize} объединены",
                passNumber: passNumber));
            passNumber++;
            runSize *= 2;
        }

        actions.Add(new ExternalSortAction(
            ExternalSortActionType.Finished,
            message: "Прямое слияние завершено"));
        return actions;
    }

    private static IReadOnlyList<ExternalSortAction> BuildNatural(
        List<int> order,
        IReadOnlyDictionary<int, CsvRowData> lookup,
        int keyColumnIndex,
        string columnLabel) {
        var actions = new List<ExternalSortAction>();
        var passNumber = 1;

        while (true) {
            var runs = DetectNaturalRuns(order, lookup, keyColumnIndex);
            if (runs.Count <= 1) {
                break;
            }

            for (var i = 0; i + 1 < runs.Count; i += 2) {
                var first = runs[i];
                var second = runs[i + 1];
                MergeRuns(order, lookup, first.Start, first.End, second.End, keyColumnIndex, columnLabel, actions);
            }

            actions.Add(new ExternalSortAction(
                ExternalSortActionType.PassComplete,
                message: $"Естественное слияние #{passNumber}: объединено серий — {runs.Count}",
                passNumber: passNumber));
            passNumber++;
        }

        actions.Add(new ExternalSortAction(
            ExternalSortActionType.Finished,
            message: "Естественное слияние завершено"));
        return actions;
    }

    private static IReadOnlyList<ExternalSortAction> BuildMultiway(
        List<int> order,
        IReadOnlyDictionary<int, CsvRowData> lookup,
        int keyColumnIndex,
        string columnLabel) {
        var actions = new List<ExternalSortAction>();
        var n = order.Count;
        var runSize = 1;
        var passNumber = 1;
        const int fanIn = 3;

        while (runSize < n) {
            for (var start = 0; start < n; start += fanIn * runSize) {
                var firstEnd = Math.Min(start + runSize, n);
                var secondEnd = Math.Min(firstEnd + runSize, n);
                var thirdEnd = Math.Min(secondEnd + runSize, n);

                if (firstEnd > start && secondEnd > firstEnd) {
                    MergeRuns(order, lookup, start, firstEnd, secondEnd, keyColumnIndex, columnLabel, actions);
                }

                if (thirdEnd > secondEnd) {
                    MergeRuns(order, lookup, start, secondEnd, thirdEnd, keyColumnIndex, columnLabel, actions);
                }
            }

            actions.Add(new ExternalSortAction(
                ExternalSortActionType.PassComplete,
                message: $"Многопутевой проход #{passNumber}: серия ×{fanIn}",
                passNumber: passNumber));
            passNumber++;
            runSize *= fanIn;
        }

        actions.Add(new ExternalSortAction(
            ExternalSortActionType.Finished,
            message: "Многопутевое слияние завершено"));
        return actions;
    }

    private static void MergeRuns(
        List<int> order,
        IReadOnlyDictionary<int, CsvRowData> lookup,
        int start,
        int mid,
        int end,
        int keyColumnIndex,
        string columnLabel,
        ICollection<ExternalSortAction> actions) {
        if (start >= mid || mid >= end) {
            return;
        }

        var left = start;
        var right = mid;

        while (left < right && right < end) {
            var leftId = order[left];
            var rightId = order[right];
            var leftKey = GetKeyValue(lookup[leftId], keyColumnIndex);
            var rightKey = GetKeyValue(lookup[rightId], keyColumnIndex);

            actions.Add(new ExternalSortAction(
                ExternalSortActionType.Compare,
                rowIdA: leftId,
                rowIdB: rightId,
                valueA: leftKey,
                valueB: rightKey,
                message: $"Сравниваем \"{Truncate(leftKey)}\" и \"{Truncate(rightKey)}\" ({columnLabel})"));

            if (CompareKeys(leftKey, rightKey) <= 0) {
                left++;
                continue;
            }

            var movingId = rightId;
            var preview = lookup[movingId].BuildPreview();
            actions.Add(new ExternalSortAction(
                ExternalSortActionType.Move,
                rowIdA: movingId,
                sourceIndex: right,
                targetIndex: left,
                valueA: preview,
                message: $"Переносим строку \"{Truncate(preview)}\" ближе к началу серии"));

            order.RemoveAt(right);
            order.Insert(left, movingId);

            left++;
            right++;
            mid++;
        }
    }

    private static List<RunRange> DetectNaturalRuns(
        IReadOnlyList<int> order,
        IReadOnlyDictionary<int, CsvRowData> lookup,
        int keyColumnIndex) {
        var runs = new List<RunRange>();
        if (order.Count == 0) {
            return runs;
        }

        var start = 0;
        for (var i = 1; i < order.Count; i++) {
            var prevKey = GetKeyValue(lookup[order[i - 1]], keyColumnIndex);
            var currentKey = GetKeyValue(lookup[order[i]], keyColumnIndex);
            if (CompareKeys(prevKey, currentKey) <= 0) {
                continue;
            }

            runs.Add(new RunRange(start, i));
            start = i;
        }

        runs.Add(new RunRange(start, order.Count));
        return runs;
    }

    private static string GetKeyValue(CsvRowData row, int keyColumnIndex) =>
        row.GetCell(keyColumnIndex)?.Trim() ?? string.Empty;

    private static int CompareKeys(string? left, string? right) {
        var leftValue = left?.Trim() ?? string.Empty;
        var rightValue = right?.Trim() ?? string.Empty;

        if (double.TryParse(leftValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftNumber) &&
            double.TryParse(rightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightNumber)) {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(leftValue, rightValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string? text, int limit = 30) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        return text.Length <= limit ? text : text[..limit] + "...";
    }

    private readonly record struct RunRange(int Start, int End);
}