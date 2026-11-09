using System;
using Avalonia;

namespace Viewer
{
    internal static class Program
    {
        // Usage: Viewer [path-to-gp-file]   (defaults to gp/test.gp next to the executable)
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }
    }
}
