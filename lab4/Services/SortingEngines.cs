using System;
using System.Collections.Generic;
using System.Linq;
using lab4.Models;

namespace lab4.Services;

public static class SortingEngines {
    public static IReadOnlyList<SortAction> BuildActions(int[] source, SortAlgorithm algorithm) {
        var prepared = source?.ToArray() ?? Array.Empty<int>();
        if (prepared.Length == 0) {
            return new List<SortAction> { new(SortActionType.Finished, message: "Массив пуст — сортировка не требуется") };
        }

        var steps = algorithm switch {
            SortAlgorithm.Bubble => BuildBubble(prepared),
            SortAlgorithm.Insertion => BuildInsertion(prepared),
            SortAlgorithm.Heap => BuildHeap(prepared),
            SortAlgorithm.Quick => BuildQuick(prepared),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };

        return steps;
    }

    private static List<SortAction> BuildBubble(int[] array) {
        var actions = new List<SortAction>();
        var n = array.Length;

        for (var i = 0; i < n; i++) {
            for (var j = 0; j < n - i - 1; j++) {
                actions.Add(new SortAction(
                    SortActionType.Compare,
                    j,
                    j + 1,
                    array[j],
                    array[j + 1],
                    $"Сравниваем {array[j]} и {array[j + 1]}"));

                if (array[j] <= array[j + 1]) {
                    continue;
                }

                var left = array[j];
                var right = array[j + 1];
                (array[j], array[j + 1]) = (right, left);
                actions.Add(new SortAction(
                    SortActionType.Swap,
                    j,
                    j + 1,
                    left,
                    right,
                    $"Обмен: {left} ↔ {right}"));
            }

            actions.Add(new SortAction(
                SortActionType.PassComplete,
                n - i - 1,
                message: $"Правый край (индекс {n - i - 1}) упорядочен"));
        }

        actions.Add(new SortAction(SortActionType.Finished, message: "Пузырёк завершён"));
        return actions;
    }

    private static List<SortAction> BuildInsertion(int[] array) {
        var actions = new List<SortAction>();
        var n = array.Length;

        for (var i = 1; i < n; i++) {
            var j = i - 1;
            while (j >= 0) {
                actions.Add(new SortAction(
                    SortActionType.Compare,
                    j,
                    j + 1,
                    array[j],
                    array[j + 1],
                    $"Проверяем, нужно ли переместить {array[j + 1]} левее {array[j]}"));

                if (array[j] <= array[j + 1]) {
                    break;
                }

                var left = array[j];
                var right = array[j + 1];
                (array[j], array[j + 1]) = (right, left);
                actions.Add(new SortAction(
                    SortActionType.Swap,
                    j,
                    j + 1,
                    left,
                    right,
                    $"Сдвигаем {right} левее, обменивая с {left}"));
                j--;
            }

            actions.Add(new SortAction(
                SortActionType.PassComplete,
                i,
                indexB: 0,
                message: $"Элементы 0..{i} находятся на своих местах"));
        }

        actions.Add(new SortAction(SortActionType.Finished, message: "Вставки завершены"));
        return actions;
    }

    private static List<SortAction> BuildHeap(int[] array) {
        var actions = new List<SortAction>();
        var n = array.Length;

        for (var i = n / 2 - 1; i >= 0; i--) {
            Heapify(array, n, i, actions);
        }

        for (var end = n - 1; end > 0; end--) {
            actions.Add(new SortAction(
                SortActionType.Swap,
                0,
                end,
                array[0],
                array[end],
                $"Максимум {array[0]} меняем с последним неотсортированным {array[end]}"));
            (array[0], array[end]) = (array[end], array[0]);
            actions.Add(new SortAction(
                SortActionType.PassComplete,
                end,
                message: $"Хвост начиная с индекса {end} упорядочен"));
            Heapify(array, end, 0, actions);
        }

        if (n > 0) {
            actions.Add(new SortAction(
                SortActionType.PassComplete,
                0,
                message: "Корень пирамиды зафиксирован"));
        }

        actions.Add(new SortAction(SortActionType.Finished, message: "Пирамидальная сортировка завершена"));
        return actions;
    }

    private static void Heapify(IList<int> array, int length, int root, ICollection<SortAction> actions) {
        var largest = root;
        var left = 2 * root + 1;
        var right = left + 1;

        if (left < length) {
            actions.Add(new SortAction(
                SortActionType.Compare,
                root,
                left,
                array[root],
                array[left],
                $"Сравнение родителя {array[root]} и левого сына {array[left]}"));
            if (array[left] > array[largest]) {
                largest = left;
            }
        }

        if (right < length) {
            actions.Add(new SortAction(
                SortActionType.Compare,
                largest,
                right,
                array[largest],
                array[right],
                $"Сравнение текущего максимума {array[largest]} и правого сына {array[right]}"));
            if (array[right] > array[largest]) {
                largest = right;
            }
        }

        if (largest == root) {
            return;
        }

        var first = array[root];
        var second = array[largest];
        (array[root], array[largest]) = (second, first);
        actions.Add(new SortAction(
            SortActionType.Swap,
            root,
            largest,
            first,
            second,
            $"Продвигаем {second} вверх, меняя с {first}"));

        Heapify(array, length, largest, actions);
    }

    private static List<SortAction> BuildQuick(int[] array) {
        var actions = new List<SortAction>();
        QuickSort(array, 0, array.Length - 1, actions);
        actions.Add(new SortAction(SortActionType.Finished, message: "Быстрая сортировка завершена"));
        return actions;
    }

    private static void QuickSort(int[] array, int low, int high, ICollection<SortAction> actions) {
        if (low >= high) {
            if (low == high && low >= 0) {
                actions.Add(new SortAction(
                    SortActionType.PassComplete,
                    low,
                    message: $"Элемент с индексом {low} уже стоит правильно"));
            }

            return;
        }

        var pivotIndex = Partition(array, low, high, actions);
        QuickSort(array, low, pivotIndex - 1, actions);
        QuickSort(array, pivotIndex + 1, high, actions);
    }

    private static int Partition(int[] array, int low, int high, ICollection<SortAction> actions) {
        var pivotValue = array[high];
        actions.Add(new SortAction(
            SortActionType.PivotSelect,
            high,
            valueA: pivotValue,
            message: $"Опорный элемент = {pivotValue} (индекс {high})"));

        var smallerIndex = low - 1;
        for (var j = low; j < high; j++) {
            actions.Add(new SortAction(
                SortActionType.Compare,
                j,
                high,
                array[j],
                pivotValue,
                $"Сравниваем {array[j]} с опорным {pivotValue}"));

            if (array[j] > pivotValue) {
                continue;
            }

            smallerIndex++;
            if (smallerIndex == j) {
                continue;
            }

            var left = array[smallerIndex];
            var right = array[j];
            (array[smallerIndex], array[j]) = (right, left);
            actions.Add(new SortAction(
                SortActionType.Swap,
                smallerIndex,
                j,
                left,
                right,
                $"Элемент {right} переносим в левую часть"));
        }

        if (smallerIndex + 1 != high) {
            var first = array[smallerIndex + 1];
            var second = array[high];
            (array[smallerIndex + 1], array[high]) = (second, first);
            actions.Add(new SortAction(
                SortActionType.Swap,
                smallerIndex + 1,
                high,
                first,
                second,
                $"Помещаем опорный {second} в позицию {smallerIndex + 1}"));
        }

        actions.Add(new SortAction(
            SortActionType.PassComplete,
            smallerIndex + 1,
            message: $"Опорный элемент ({array[smallerIndex + 1]}) зафиксирован"));

        return smallerIndex + 1;
    }
}
