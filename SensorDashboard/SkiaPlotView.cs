using System;
using System.IO;
using Gtk;
using OxyPlot;
using OxyPlot.SkiaSharp;

namespace SensorDashboard;

internal sealed class SkiaPlotView : Image
{
    private PlotModel? _model;
    private int _width;
    private int _height;

    public PlotModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            RenderPlot();
        }
    }

    protected override void OnSizeAllocated(Gdk.Rectangle allocation)
    {
        base.OnSizeAllocated(allocation);

        if (allocation.Width <= 0 || allocation.Height <= 0)
        {
            return;
        }

        if (allocation.Width != _width || allocation.Height != _height)
        {
            _width = allocation.Width;
            _height = allocation.Height;
            RenderPlot();
        }
    }

    public void RenderPlot()
    {
        if (_model is null || _width <= 0 || _height <= 0)
        {
            return;
        }

        using var buffer = new MemoryStream();
        var exporter = new PngExporter
        {
            Width = Math.Max(_width, 1),
            Height = Math.Max(_height, 1),
            Dpi = 96,
            UseTextShaping = true
        };

        exporter.Export(_model, buffer);
        buffer.Position = 0;

        using var pixbuf = new Gdk.Pixbuf(buffer);
        Pixbuf = pixbuf.Copy();
    }

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
