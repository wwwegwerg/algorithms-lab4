using System.Collections.Generic;

namespace lab4.Charts;

public class ChartData {
    public readonly string Title;
    public readonly IList<(string SeriesTitile, IList<DataPoint> Mesuarements)> Results;
    public readonly string XAxisTitle;
    public readonly string YAxisTitle;
    public readonly double? TotalExecTimeSeconds;

    public ChartData(
        string title,
        IList<(string, IList<DataPoint>)> results,
        string xAxisTitle,
        string yAxisTitle,
        double? totalExecTimeSeconds = null) {
        Title = title;
        Results = results;
        XAxisTitle = xAxisTitle;
        YAxisTitle = yAxisTitle;
        TotalExecTimeSeconds = totalExecTimeSeconds;
    }
}