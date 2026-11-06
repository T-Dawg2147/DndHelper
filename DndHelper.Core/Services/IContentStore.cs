using DndHelper.Core.Models;

namespace DndHelper.Core.Services;

public interface IContentStore
{
    void Initialize();

    Task<int> CountCreaturesAsync(CancellationToken ct = default);
    Task<int> CountSpellsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CreatureSummary>> SearchCreaturesAsync(CreatureQuery query, CancellationToken ct = default);
    Task<Creature?> GetCreatureAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCreatureTypesAsync(CancellationToken ct = default);
    
    Task<IReadOnlyList<SpellSummary>> SearchSpellsAsync(SpellQuery query, CancellationToken ct = default);
    Task<Spell?> GetSpellAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetSpellSchoolsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetSpellClassesAsync(CancellationToken ct = default);
    
    Task ReplaceContentAsync(IReadOnlyCollection<Creature> creatures, IReadOnlyCollection<Spell> spells, CancellationToken ct = default);
}