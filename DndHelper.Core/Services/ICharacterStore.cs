using DndHelper.Core.Characters;

namespace DndHelper.Core.Services;

/// <summary>Persistent storage for player characters.</summary>
public interface ICharacterStore
{
    Task<IReadOnlyList<PlayerCharacter>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Creates the character, or replaces the saved one with the same Id.</summary>
    Task SaveAsync(PlayerCharacter character, CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
}