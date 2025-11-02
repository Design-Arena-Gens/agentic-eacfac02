using Gtk;

namespace SensorDashboard;

internal static class Program
{
    public static void Main(string[] args)
    {
        Application.Init();

        using var app = new Application("com.example.SensorDashboard", GLib.ApplicationFlags.None);
        app.Register(GLib.Cancellable.Current);

        var window = new MainWindow();
        app.AddWindow(window);
        window.ShowAll();

        Application.Run();
    }
}
