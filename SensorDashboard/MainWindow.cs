using System.Linq;
using GLib;
using Gtk;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace SensorDashboard;

internal sealed class MainWindow : Window
{
    private readonly SensorRepository _repository = new();
    private readonly ListStore _sensorStore;
    private readonly Dictionary<string, PlotModel> _plotModels = new();
    private readonly Dictionary<string, LineSeries> _plotSeries = new();
    private readonly Dictionary<string, SkiaPlotView> _plotViews = new();

    private TreeView _sensorTreeView = null!;
    private Label _detailLabel = null!;
    private readonly Label _summaryLabel;
    private readonly Label _alertsLabel;

    public MainWindow()
        : base("Панель мониторинга датчиков")
    {
        DefaultSize = new Gdk.Size(1280, 820);
        BorderWidth = 8;
        WindowPosition = WindowPosition.Center;

        DeleteEvent += (_, _) => Gtk.Application.Quit();

        _sensorStore = new ListStore(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string));
        _summaryLabel = new Label { Xalign = 0, Wrap = true, UseMarkup = true, MarginStart = 8, MarginEnd = 8, MarginTop = 6, MarginBottom = 6 };
        _alertsLabel = new Label { Xalign = 0, Wrap = true, UseMarkup = true, MarginStart = 8, MarginEnd = 8, MarginTop = 6, MarginBottom = 6 };

        var root = new Box(Orientation.Vertical, 6);
        Add(root);

        root.PackStart(CreateMenuBar(), false, false, 0);

        var content = new Box(Orientation.Vertical, 6);
        root.PackStart(content, true, true, 0);

        var mainSplit = new Paned(Orientation.Horizontal)
        {
            Position = 320
        };

        mainSplit.Pack1(CreateSensorPanel(), false, false);
        mainSplit.Pack2(CreateChartsNotebook(), true, false);
        content.PackStart(mainSplit, true, true, 0);

        var bottomPanel = CreateBottomPanel();
        bottomPanel.SetSizeRequest(-1, 200);
        content.PackStart(bottomPanel, false, false, 0);

        RefreshUi();

        ShowAll();

        GLib.Timeout.Add(4000, () =>
        {
            _repository.AdvanceSimulation();
            RefreshUi();
            return true;
        });
    }

    private MenuBar CreateMenuBar()
    {
        var menuBar = new MenuBar();

        var fileMenu = new Gtk.Menu();
        var fileItem = new Gtk.MenuItem("Файл") { Submenu = fileMenu };

        var refreshItem = new Gtk.MenuItem("Обновить");
        refreshItem.Activated += (_, _) => RefreshUi();

        var stepItem = new Gtk.MenuItem("Шаг симуляции");
        stepItem.Activated += (_, _) =>
        {
            for (var i = 0; i < 5; i++)
            {
                _repository.AdvanceSimulation();
            }

            RefreshUi();
        };

        var quitItem = new Gtk.MenuItem("Выход");
        quitItem.Activated += (_, _) => Gtk.Application.Quit();

        fileMenu.Append(refreshItem);
        fileMenu.Append(stepItem);
        fileMenu.Append(new SeparatorMenuItem());
        fileMenu.Append(quitItem);

        var viewMenu = new Gtk.Menu();
        var viewItem = new Gtk.MenuItem("Вид") { Submenu = viewMenu };

        var resetChartsItem = new Gtk.MenuItem("Сбросить масштаб графиков");
        resetChartsItem.Activated += (_, _) =>
        {
            foreach (var model in _plotModels.Values)
            {
                model.ResetAllAxes();
                model.InvalidatePlot(true);
            }
        };

        viewMenu.Append(resetChartsItem);

        menuBar.Append(fileItem);
        menuBar.Append(viewItem);

        return menuBar;
    }

    private Widget CreateSensorPanel()
    {
        var container = new Box(Orientation.Vertical, 6);

        _sensorTreeView = new TreeView(_sensorStore)
        {
            HeadersVisible = true,
            EnableGridLines = TreeViewGridLines.Horizontal,
            FixedHeightMode = false
        };

        var nameColumn = new TreeViewColumn("Датчик", new CellRendererText(), "text", 0);
        var valueColumn = new TreeViewColumn("Значение", new CellRendererText(), "text", 1);
        var statusRenderer = new CellRendererText();
        var statusColumn = new TreeViewColumn("Статус", statusRenderer, "text", 2);
        statusColumn.AddAttribute(statusRenderer, "foreground", 3);

        _sensorTreeView.AppendColumn(nameColumn);
        _sensorTreeView.AppendColumn(valueColumn);
        _sensorTreeView.AppendColumn(statusColumn);

        _sensorTreeView.Selection.Mode = Gtk.SelectionMode.Single;
        _sensorTreeView.Selection.Changed += (_, _) => UpdateSensorDetails();

        var scroller = new ScrolledWindow
        {
            ShadowType = ShadowType.In,
            Expand = true
        };
        scroller.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
        scroller.Add(_sensorTreeView);

        var listFrame = new Frame("Датчики");
        listFrame.Add(scroller);

        _detailLabel = new Label
        {
            Wrap = true,
            Xalign = 0,
            UseMarkup = true,
            MarginStart = 8,
            MarginEnd = 8,
            MarginTop = 4,
            MarginBottom = 4
        };

        var detailFrame = new Frame("Информация")
        {
            MarginTop = 4
        };

        var detailBox = new Box(Orientation.Vertical, 0);
        detailBox.PackStart(_detailLabel, false, false, 0);
        detailFrame.Add(detailBox);

        container.PackStart(listFrame, true, true, 0);
        container.PackStart(detailFrame, false, false, 0);

        return container;
    }

    private Notebook CreateChartsNotebook()
    {
        var notebook = new Notebook
        {
            TabPos = PositionType.Top,
            Scrollable = true
        };

        foreach (var sensor in _repository.Sensors.Take(3))
        {
            var plotView = CreatePlot(sensor);
            notebook.AppendPage(plotView, new Label(sensor.Name));
        }

        return notebook;
    }

    private Widget CreateBottomPanel()
    {
        var summariesFrame = new Frame("Сводка по датчикам");
        var alertsFrame = new Frame("Предупреждения");

        var summaryBox = new Box(Orientation.Vertical, 0);
        summaryBox.PackStart(_summaryLabel, false, false, 0);
        summariesFrame.Add(summaryBox);

        var alertBox = new Box(Orientation.Vertical, 0);
        alertBox.PackStart(_alertsLabel, false, false, 0);
        alertsFrame.Add(alertBox);

        var container = new Box(Orientation.Horizontal, 6)
        {
            Homogeneous = true
        };

        container.PackStart(summariesFrame, true, true, 0);
        container.PackStart(alertsFrame, true, true, 0);

        return container;
    }

    private SkiaPlotView CreatePlot(Sensor sensor)
    {
        var model = new PlotModel
        {
            Title = sensor.Name,
            Subtitle = sensor.Location,
            PlotAreaBorderColor = OxyColors.Gray,
            Background = OxyColors.WhiteSmoke
        };

        var timeAxis = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm",
            IntervalType = DateTimeIntervalType.Minutes,
            MinorIntervalType = DateTimeIntervalType.Minutes,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot
        };

        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = $"{sensor.Name} ({sensor.Unit})",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            Minimum = sensor.Minimum - (sensor.Maximum - sensor.Minimum) * 0.2,
            Maximum = sensor.Maximum + (sensor.Maximum - sensor.Minimum) * 0.2
        };

        var lineSeries = new LineSeries
        {
            Color = OxyColors.DodgerBlue,
            LineStyle = LineStyle.Solid,
            StrokeThickness = 2,
            MarkerSize = 2,
            CanTrackerInterpolatePoints = false
        };

        model.Axes.Add(timeAxis);
        model.Axes.Add(valueAxis);
        model.Series.Add(lineSeries);

        _plotModels[sensor.Id] = model;
        _plotSeries[sensor.Id] = lineSeries;

        var plotView = new SkiaPlotView
        {
            Model = model,
            TooltipText = sensor.Name,
            Expand = true
        };

        _plotViews[sensor.Id] = plotView;

        UpdateSeries(sensor.Id);

        return plotView;
    }

    private void RefreshUi()
    {
        var selectedId = GetSelectedSensorId();

        _sensorStore.Clear();

        TreeIter? selectedIter = null;
        foreach (var sensor in _repository.Sensors)
        {
            var iter = _sensorStore.AppendValues(
                sensor.Name,
                $"{sensor.CurrentValue:F2} {sensor.Unit}",
                StatusToText(sensor.CurrentStatus),
                StatusToColor(sensor.CurrentStatus),
                sensor.Id);

            if (sensor.Id == selectedId)
            {
                selectedIter = iter;
            }
        }

        if (selectedIter.HasValue)
        {
            _sensorTreeView.Selection.SelectIter(selectedIter.Value);
        }
        else
        {
            _sensorTreeView.Selection.SelectPath(new TreePath("0"));
        }

        foreach (var sensor in _repository.Sensors.Take(3))
        {
            UpdateSeries(sensor.Id);
            if (_plotViews.TryGetValue(sensor.Id, out var view))
            {
                view.RenderPlot();
            }
        }

        UpdateSummary();
        UpdateAlerts();
        UpdateSensorDetails();
    }

    private void UpdateSeries(string sensorId)
    {
        if (!_plotSeries.TryGetValue(sensorId, out var series) ||
            !_plotModels.TryGetValue(sensorId, out var model))
        {
            return;
        }

        var history = _repository.GetHistory(sensorId);
        series.Points.Clear();

        foreach (var reading in history)
        {
            var x = DateTimeAxis.ToDouble(reading.Timestamp.ToLocalTime());
            series.Points.Add(new DataPoint(x, reading.Value));
        }

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) is DateTimeAxis timeAxis &&
            history.Count > 0)
        {
            timeAxis.Minimum = DateTimeAxis.ToDouble(history.First().Timestamp.ToLocalTime());
            timeAxis.Maximum = DateTimeAxis.ToDouble(history.Last().Timestamp.ToLocalTime());
        }

        if (model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) is LinearAxis valueAxis)
        {
            var sensor = _repository.Sensors.First(x => x.Id == sensorId);
            valueAxis.Minimum = sensor.Minimum - (sensor.Maximum - sensor.Minimum) * 0.2;
            valueAxis.Maximum = sensor.Maximum + (sensor.Maximum - sensor.Minimum) * 0.2;
        }

        model.InvalidatePlot(true);
    }

    private void UpdateSummary()
    {
        var lines = _repository.Sensors
            .Select(sensor =>
                $"<b>{Markup.EscapeText(sensor.Name)}</b>: {sensor.CurrentValue:F2} {Markup.EscapeText(sensor.Unit)} · {StatusToText(sensor.CurrentStatus)}");

        _summaryLabel.Markup = string.Join("\n", lines);
    }

    private void UpdateAlerts()
    {
        var alerts = _repository.GetAlerts().ToList();
        if (alerts.Count == 0)
        {
            _alertsLabel.Markup = "<span foreground=\"#1b8733\">Отклонений не обнаружено.</span>";
            return;
        }

        var lines = alerts.Select(sensor =>
        {
            var status = StatusToText(sensor.CurrentStatus);
            var color = StatusToColor(sensor.CurrentStatus);
            return $"<span foreground=\"{color}\"><b>{Markup.EscapeText(sensor.Name)}:</b> {sensor.CurrentValue:F2} {Markup.EscapeText(sensor.Unit)} · {status}</span>";
        });

        _alertsLabel.Markup = string.Join("\n", lines);
    }

    private void UpdateSensorDetails()
    {
        var id = GetSelectedSensorId();
        if (id is null)
        {
            _detailLabel.Markup = "Выберите датчик для просмотра подробностей.";
            return;
        }

        var sensor = _repository.Sensors.FirstOrDefault(s => s.Id == id);
        if (sensor is null)
        {
            _detailLabel.Markup = "Выберите датчик для просмотра подробностей.";
            return;
        }

        var recent = _repository.GetRecentHistory(sensor.Id, 5)
            .Select(r => $"{r.Timestamp.ToLocalTime():HH:mm} — {r.Value:F2} {sensor.Unit}");

        _detailLabel.Markup =
            $"<b>{Markup.EscapeText(sensor.Name)}</b>\n" +
            $"Локация: {Markup.EscapeText(sensor.Location)}\n" +
            $"Номинал: {sensor.Nominal:F2} {Markup.EscapeText(sensor.Unit)}\n" +
            $"Диапазон: {sensor.Minimum:F2} – {sensor.Maximum:F2} {Markup.EscapeText(sensor.Unit)}\n" +
            $"Статус: <span foreground=\"{StatusToColor(sensor.CurrentStatus)}\">{StatusToText(sensor.CurrentStatus)}</span>\n\n" +
            "<b>Последние показания:</b>\n" +
            string.Join("\n", recent);
    }

    private string? GetSelectedSensorId()
    {
        if (!_sensorTreeView.Selection.GetSelected(out var model, out var iter))
        {
            return null;
        }

        return (string)model.GetValue(iter, 4);
    }

    private static string StatusToText(SensorStatus status) =>
        status switch
        {
            SensorStatus.Normal => "Норма",
            SensorStatus.Caution => "Отклонение",
            SensorStatus.Low => "Ниже порога",
            SensorStatus.High => "Выше порога",
            _ => "Неизвестно"
        };

    private static string StatusToColor(SensorStatus status) =>
        status switch
        {
            SensorStatus.Normal => "#1b8733",
            SensorStatus.Caution => "#e5a200",
            SensorStatus.Low => "#006dd6",
            SensorStatus.High => "#c23030",
            _ => "#5a5a5a"
        };
}
