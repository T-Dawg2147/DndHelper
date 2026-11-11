using DndHelper.Core.Services;
using DndHelper.Data.Open5e;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DndHelper.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the SQLite content cache, the Open5e sync service and the character store.</summary>
    /// <param name="databasePath">Optional database file path. Defaults to %LOCALAPPDATA%\DndHelper\content.db.</param>
    /// <param name="charactersPath">Optional character folder. Defaults to %LOCALAPPDATA%\DndHelper\characters.</param>
    public static IServiceCollection AddDndHelperData(
        this IServiceCollection services,
        string? databasePath = null,
        Action<Open5eOptions>? configureOpen5e = null,
        string? charactersPath = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DndHelper");

        databasePath ??= Path.Combine(root, "content.db");
        charactersPath ??= Path.Combine(root, "characters");

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var options = new Open5eOptions();
        configureOpen5e?.Invoke(options);

        services.AddDbContextFactory<ContentDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<IContentStore, ContentStore>();

        services.AddSingleton<ICharacterStore>(sp =>
            new JsonCharacterStore(charactersPath, sp.GetRequiredService<ILogger<JsonCharacterStore>>()));

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