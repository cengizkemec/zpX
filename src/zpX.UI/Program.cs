using System;
using System.Windows;
using Velopack;

namespace zpX.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            VelopackApp.Build().Run();
        }
        catch
        {
            // Running unpackaged (development): Velopack is a no-op.
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
