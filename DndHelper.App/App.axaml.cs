using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DndHelper.App.Docking;
using DndHelper.App.View;
using DndHelper.App.ViewModels;
using DndHelper.Core.Services;
using DndHelper.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DndHelper.App;

public class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        Services = _host.Services;
        Services.GetRequiredService<IContentStore>().Initialize();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow()
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.Exit += (_, _) => _host.Dispose();
        }
        
        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddDndHelperData();
        services.AddSingleton<DockFactory>();
        services.AddSingleton<MainWindowViewModel>();
    }
}