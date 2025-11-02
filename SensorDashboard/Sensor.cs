namespace SensorDashboard;

internal sealed class Sensor
{
    public Sensor(
        string id,
        string name,
        string unit,
        string location,
        double nominal,
        double minimum,
        double maximum)
    {
        Id = id;
        Name = name;
        Unit = unit;
        Location = location;
        Nominal = nominal;
        Minimum = minimum;
        Maximum = maximum;
    }

    public string Id { get; }
    public string Name { get; }
    public string Unit { get; }
    public string Location { get; }
    public double Nominal { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double CurrentValue { get; set; }

    public SensorStatus CurrentStatus => CurrentValue switch
    {
        var value when value < Minimum => SensorStatus.Low,
        var value when value > Maximum => SensorStatus.High,
        var value when value < Minimum + (Nominal - Minimum) * 0.1 => SensorStatus.Caution,
        var value when value > Maximum - (Maximum - Nominal) * 0.1 => SensorStatus.Caution,
        _ => SensorStatus.Normal
    };
}
