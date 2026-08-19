namespace EpsilonPlotter;

public struct DataSeries(string legend, List<double> x, List<double> y)
{
    public string Legend = legend;
    public List<double> X = x;
    public List<double> Y = y;
}
