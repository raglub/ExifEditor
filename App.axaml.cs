using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ExifEditor.Services;
using ExifEditor.ViewModels;
using ExifEditor.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExifEditor;

public partial class App : Application
{
    public static IHost? AppHost {get; private set;}

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<MainWindow>();
                services.AddTransient<DirectoryService>();
                services.AddSingleton<ServiceFactory>();
                services.AddTransient<ImageService>();
                services.AddTransient<PdfGeneratorService>();
                services.AddSingleton<ThemeService>();
                services.AddTransient<MainWindowViewModel>();
            }).Build();
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var themeService = AppHost!.Services.GetRequiredService<ThemeService>();

        var settings = SettingsService.LoadSettings();
        var savedTheme = settings.Theme switch
        {
            "VioletCyan" => AppTheme.VioletCyan,
            _ => AppTheme.OceanBlue
        };
        themeService.ApplyTheme(savedTheme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = AppHost!.Services.GetRequiredService<MainWindow>();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
