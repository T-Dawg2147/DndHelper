using DndHelper.Core.Services;
using DndHelper.Data.Open5e;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DndHelper.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddDndHelperData(
        this IServiceCollection services,
        string? databasePath = null,
        Action<Open5eOptions>? configureOpen5e = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DndHelper", "content.db");

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var options = new Open5eOptions();
        configureOpen5e?.Invoke(options);

        services.AddDbContextFactory<ContentDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<IContentStore, ContentStore>();

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddHttpClient<Open5eClient>(http =>
        {
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = TimeSpan.FromMinutes(2);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DndHelper/1.0");
        });
        services.AddTransient<IContentSyncService, Open5eSyncService>();

        return services;
    }
}