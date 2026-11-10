using System;
using Avalonia;

namespace Viewer
{
    internal static class Program
    {
        // Usage: Viewer [All.gsc | extracted-folder | sprite-in-one]
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
