using System;
using System.IO;
using System.Linq;
using System.Text;
using Plotly.NET;
using GenericChart = Plotly.NET.GenericChart;

namespace lab4.Charts;

public static class ChartBuilder {
    private static readonly DateTime ProgramStartTime = DateTime.Now;

    public static string Build2DLineChart(ChartData cd, bool promptOnOverwrite = true) {
        var dataSetSize = Math.Log2(cd.Results[0].Mesuarements[^1].X);
        Console.WriteLine($"{cd.Title} – {dataSetSize} – {cd.TotalExecTimeSeconds}s");

        var outputDir = Path.Combine(AppContext.BaseDirectory, "plots");
        Directory.CreateDirectory(outputDir);
        var fileName = Sanitize($"{cd.Title} - {ProgramStartTime:s} - {cd.TotalExecTimeSeconds}s.html");
        var filePath = Path.Combine(outputDir, fileName);

        if (File.Exists(filePath) && promptOnOverwrite) {
            Console.WriteLine($"Файл {filePath} уже существует. Перезаписать? (y/n)");
            while (true) {
                var choice = Console.ReadLine()?.Trim().ToLower();
                if (choice == "y") {
                    break;
                }

                if (choice == "n") {
                    return filePath;
                }

                Console.WriteLine("Пожалуйста, введите 'y' или 'n'.");
            }
        }

        var gCharts = new GenericChart[cd.Results.Count];

        for (var i = 0; i < gCharts.Length; i++) {
            var result = cd.Results[i];
            gCharts[i] = Chart2D.Chart
                .Line<double, double, string>(
                    result.Mesuarements.Select(p => p.X),
                    result.Mesuarements.Select(p => p.Y),
                    Name: result.SeriesTitile,
                    ShowLegend: true,
                    LineWidth: 2.5,
                    ShowMarkers: true);
        }

        var chart = Chart.Combine(gCharts)
            .WithTitle($"{cd.Title} – {dataSetSize} – {cd.TotalExecTimeSeconds}s")
            .WithXAxisStyle(Title.init(cd.XAxisTitle))
            .WithYAxisStyle(Title.init(cd.YAxisTitle))
            .WithConfig(Config.init(Responsive: true));

        var html = GenericChart.toEmbeddedHTML(chart).Replace(
            "<title>Plotly.NET Datavisualization</title>",
            $"<title>{Sanitize(cd.Title)}.html</title>"
        );
        html = EnsureResponsiveUtf8Head(html);
        File.WriteAllText(filePath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Console.WriteLine("Готово!");
        Console.WriteLine($"Файл сохранён: {filePath}");
        return filePath;
    }

    private static string Sanitize(string name) {
        var bad = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray());
    }

    private static string EnsureResponsiveUtf8Head(string html) {
        const string head = @"
<meta charset=""utf-8""/>
<meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no""/>
<style>
  html, body {
    height: 100%;
    width: 100%;
    margin: 0; padding: 0;
    overflow: hidden;            /* WKWebView любит скролл — уберём */
  }
  /* plotly контейнеры — на весь доступный размер */
  .js-plotly-plot, .plot-container, .svg-container, .main-svg, .plotly {
    width: 100% !important;
    height: 100% !important;
  }
</style>
<script>
(function(){
  function resizePlots(){
    try {
      var w = document.documentElement.clientWidth;
      var h = document.documentElement.clientHeight;
      var plots = document.getElementsByClassName('js-plotly-plot');
      for (var i = 0; i < plots.length; i++) {
        // подстраховочно обновим размеры контейнера
        plots[i].style.width  = w + 'px';
        plots[i].style.height = h + 'px';
        Plotly.relayout(plots[i], { autosize: true });
        Plotly.Plots.resize(plots[i]);
      }
    } catch(e) { /* noop */ }
  }

  // ResizeObserver даёт самые стабильные ресайзы в WKWebView
  if (typeof ResizeObserver !== 'undefined') {
    var ro = new ResizeObserver(resizePlots);
    ro.observe(document.documentElement);
  }
  window.addEventListener('resize', resizePlots);
  document.addEventListener('DOMContentLoaded', resizePlots);
  window.addEventListener('load', resizePlots);
})();
</script>";

        // если есть <head> — вставим сразу после открывающего тега
        var idxHead = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (idxHead >= 0) {
            var idxEnd = html.IndexOf('>', idxHead);
            if (idxEnd > idxHead) {
                return html.Insert(idxEnd + 1, head);
            }
        }

        // оборачиваем, если <head> отсутствует
        return $@"
<!doctype html>
<html lang=""ru"">
<head>{head}</head>
<body>
{html}
</body>
</html>";
    }
}