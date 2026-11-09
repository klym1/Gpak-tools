using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Viewer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var gpFile = desktop.Args?.FirstOrDefault() ?? MainWindow.DefaultGpFile;
                desktop.MainWindow = new MainWindow(gpFile);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
