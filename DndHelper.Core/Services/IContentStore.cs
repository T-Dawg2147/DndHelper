using DndHelper.Core.Models;

namespace DndHelper.Core.Services;

/// <summary>Local, searchable cache of rules content (creatures, spells).</summary>
public interface IContentStore
{
    /// <summary>Creates the local database if it does not exist yet. Call once at startup.</summary>
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

    /// <summary>Replaces all downloaded content in a single transaction. Custom creatures are kept.</summary>
    Task ReplaceContentAsync(IReadOnlyCollection<Creature> creatures, IReadOnlyCollection<Spell> spells, CancellationToken ct = default);

    /// <summary>Creates or updates a user-made creature. Its SourceKey must be custom.</summary>
    Task SaveCustomCreatureAsync(Creature creature, CancellationToken ct = default);

    /// <summary>Deletes a user-made creature. Downloaded creatures can never be deleted this way.</summary>
    Task<bool> DeleteCustomCreatureAsync(string key, CancellationToken ct = default);
}