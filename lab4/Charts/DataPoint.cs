namespace lab4.Charts;

public readonly struct DataPoint {
    public double X { get; }
    public double Y { get; }

    public DataPoint(double x, double y) {
        X = x;
        Y = y;
    }

    public override string ToString() {
        return X + " " + Y;
    }
}