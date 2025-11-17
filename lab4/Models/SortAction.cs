namespace lab4.Models;

public enum SortAlgorithm {
    Bubble,
    Insertion,
    Heap,
    Quick
}

public enum SortActionType {
    Compare,
    Swap,
    PassComplete,
    PivotSelect,
    Finished
}

public class SortAction {
    public SortAction(SortActionType type,
        int indexA = -1,
        int indexB = -1,
        int? valueA = null,
        int? valueB = null,
        string? message = null,
        int? passNumber = null) {
        Type = type;
        IndexA = indexA;
        IndexB = indexB;
        ValueA = valueA;
        ValueB = valueB;
        Message = message ?? string.Empty;
        PassNumber = passNumber;
    }

    public SortActionType Type { get; }
    public int IndexA { get; }
    public int IndexB { get; }
    public int? ValueA { get; }
    public int? ValueB { get; }
    public string Message { get; }
    public int? PassNumber { get; }
}