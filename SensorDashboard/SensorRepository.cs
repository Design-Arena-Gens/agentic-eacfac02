using System.Collections.ObjectModel;
using System.Linq;

namespace SensorDashboard;

internal sealed class SensorRepository
{
    private const int HistorySize = 240;
    private static readonly TimeSpan SamplingInterval = TimeSpan.FromMinutes(1);

    private readonly IReadOnlyList<Sensor> _sensors;
    private readonly Dictionary<string, LinkedList<SensorReading>> _history = new();
    private readonly Random _random = new();

    public SensorRepository()
    {
        _sensors = new List<Sensor>
        {
            new("temp-line", "Температура линии", "°C", "Цех №1", 62, 40, 85),
            new("humidity-line", "Влажность", "%", "Цех №1", 45, 25, 70),
            new("pressure-loop", "Давление", "кПа", "Контур охлаждения", 210, 160, 260),
            new("vibration", "Вибрация", "мм/с", "Пресс А-14", 2.4, 0.8, 4.5),
            new("flow", "Расход", "л/мин", "Секция подачи", 180, 120, 240)
        };

        SeedHistory();
    }

    public IReadOnlyList<Sensor> Sensors => _sensors;

    public IReadOnlyList<SensorReading> GetHistory(string sensorId)
    {
        if (!_history.TryGetValue(sensorId, out var series))
        {
            return Array.Empty<SensorReading>();
        }

        return new ReadOnlyCollection<SensorReading>(series.ToArray());
    }

    public IReadOnlyList<SensorReading> GetRecentHistory(string sensorId, int count)
    {
        if (!_history.TryGetValue(sensorId, out var series))
        {
            return Array.Empty<SensorReading>();
        }

        return new ReadOnlyCollection<SensorReading>(series.TakeLast(count).ToArray());
    }

    public IReadOnlyList<Sensor> GetAlerts() =>
        _sensors.Where(s => s.CurrentStatus is SensorStatus.Low or SensorStatus.High).ToArray();

    public void AdvanceSimulation()
    {
        foreach (var sensor in _sensors)
        {
            var next = GenerateNext(sensor);
            Enqueue(sensor.Id, next);
            sensor.CurrentValue = next.Value;
        }
    }

    private void SeedHistory()
    {
        var now = DateTime.UtcNow;
        foreach (var sensor in _sensors)
        {
            var series = new LinkedList<SensorReading>();
            var start = now - TimeSpan.FromTicks(SamplingInterval.Ticks * HistorySize);
            var value = sensor.Nominal;

            for (var i = 0; i < HistorySize; i++)
            {
                var timestamp = start + TimeSpan.FromTicks(SamplingInterval.Ticks * i);
                value = Perturb(sensor, value);
                series.AddLast(new SensorReading(timestamp, value));
            }

            sensor.CurrentValue = series.Last?.Value.Value ?? sensor.Nominal;
            _history[sensor.Id] = series;
        }
    }

    private SensorReading GenerateNext(Sensor sensor)
    {
        var previous = _history[sensor.Id].Last?.Value.Value ?? sensor.Nominal;
        var timestamp = (_history[sensor.Id].Last?.Value.Timestamp ?? DateTime.UtcNow) + SamplingInterval;
        var value = Perturb(sensor, previous);
        return new SensorReading(timestamp, value);
    }

    private double Perturb(Sensor sensor, double baseline)
    {
        var drift = (sensor.Nominal - baseline) * 0.05;
        var noise = (_random.NextDouble() - 0.5) * sensor.Nominal * 0.04;

        var adjusted = baseline + drift + noise;

        var lowerClamp = sensor.Minimum - (sensor.Maximum - sensor.Minimum) * 0.15;
        var upperClamp = sensor.Maximum + (sensor.Maximum - sensor.Minimum) * 0.15;

        return Math.Clamp(Math.Round(adjusted, 2), lowerClamp, upperClamp);
    }

    private void Enqueue(string sensorId, SensorReading reading)
    {
        var series = _history[sensorId];
        series.AddLast(reading);
        if (series.Count > HistorySize)
        {
            series.RemoveFirst();
        }
    }
}
