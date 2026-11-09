namespace DndHelper.Core.Services;

public sealed record SyncResult(int CreatureCount, int SpellCount);

public interface IContentSyncService
{
    Task<SyncResult> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default);
}