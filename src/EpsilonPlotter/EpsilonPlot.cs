using ScottPlot.Plottables;

namespace EpsilonPlotter;

public struct EpsilonPlot
{
    public required string FullFilePath { get; set; }
    public required string Title { get; set; }
    public required string AxisTitleHorizontal { get; set; }
    public required string AxisTitleVertical { get; set; }
    public required List<DataSeries> Series { get; set; }

    public readonly void Save(int width, int height)
    {
        string filePath = FullFilePath;
        ScottPlot.Plot plot = new();
        plot.Axes.Bottom.Label.Text = AxisTitleHorizontal;
        plot.Axes.Left.Label.Text = AxisTitleVertical;

        foreach (DataSeries series in Series)
        {
            Scatter scatter1 = plot.Add.Scatter(series.X, series.Y);
            scatter1.LegendText = series.Legend;
        }

        plot.SavePng(filePath, width, height);
    }
}
