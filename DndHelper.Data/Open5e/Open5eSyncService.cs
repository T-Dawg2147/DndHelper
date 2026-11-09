using System.Text.Json;
using DndHelper.Core.Models;
using DndHelper.Core.Services;
using Microsoft.Extensions.Logging;

namespace DndHelper.Data.Open5e;

public sealed class Open5eSyncService(
    Open5eClient client,
    IContentStore store,
    ILogger<Open5eSyncService> logger) : IContentSyncService
{
    public async Task<SyncResult> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var rawCreatures = await client.GetAllAsync("creatures", "creatures", progress, ct).ConfigureAwait(false);
        var rawSpells = await client.GetAllAsync("spells", "spells", progress, ct).ConfigureAwait(false);

        progress?.Report("Processing content...");
        var creatures = Map(rawCreatures, Open5eMapper.ParseCreature, c => c.Key, "creature");
        var spells = Map(rawSpells, Open5eMapper.ParseSpell, s => s.Key, "spell");

        // Never wipe a working cache if the download produced nothing.
        if (creatures.Count == 0 && spells.Count == 0)
            throw new InvalidOperationException("The server returned no creatures or spells for the configured sources.");

        progress?.Report("Saving to local database...");
        await store.ReplaceContentAsync(creatures, spells, ct).ConfigureAwait(false);

        logger.LogInformation("Synced {Creatures} creatures and {Spells} spells", creatures.Count, spells.Count);
        return new SyncResult(creatures.Count, spells.Count);
    }

    private List<T> Map<T>(IEnumerable<JsonElement> raw, Func<JsonElement, T> parse, Func<T, string> key, string kind)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var element in raw)
        {
            try
            {
                var item = parse(element);
                if (string.IsNullOrWhiteSpace(key(item)))
                {
                    logger.LogWarning("Skipping {Kind} without a key", kind);
                    continue;
                }
                map[key(item)] = item;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping malformed {Kind}", kind);
            }
        }
        return map.Values.ToList();
    }
}