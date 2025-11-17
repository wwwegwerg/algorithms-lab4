using System.Collections.Generic;
using System.Linq;

namespace lab4.Models;

public class CsvRowData {
    public CsvRowData(int id, IReadOnlyList<string> cells) {
        Id = id;
        Cells = cells?.ToList() ?? new List<string>();
    }

    public int Id { get; }

    public IReadOnlyList<string> Cells { get; }

    public string GetCell(int index) =>
        index >= 0 && index < Cells.Count ? Cells[index] : string.Empty;

    public string BuildPreview(int maxColumns = 5, int maxLength = 60) {
        var slice = Cells.Take(maxColumns);
        var joined = string.Join(" | ", slice);
        if (Cells.Count > maxColumns) {
            joined += " | ...";
        }

        if (joined.Length > maxLength) {
            joined = joined[..maxLength] + "...";
        }

        return joined;
    }
}
