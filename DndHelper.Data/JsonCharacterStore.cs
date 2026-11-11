using System.Text.Json;
using DndHelper.Core.Characters;
using DndHelper.Core.Services;
using Microsoft.Extensions.Logging;

namespace DndHelper.Data;

public sealed class JsonCharacterStore : ICharacterStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly ILogger<JsonCharacterStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonCharacterStore(string directory, ILogger<JsonCharacterStore> logger)
    {
        _directory = directory;
        _logger = logger;
        Directory.CreateDirectory(directory);
    }

    public async Task<IReadOnlyList<PlayerCharacter>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var characters = new List<PlayerCharacter>();

            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    await using var stream = File.OpenRead(file);
                    var character = await JsonSerializer.DeserializeAsync<PlayerCharacter>(stream, Options, ct).ConfigureAwait(false);
                    if (character is not null && Guid.TryParse(character.Id, out _))
                        characters.Add(character);
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    _logger.LogWarning(ex, "Skipping unreadable character file {File}", file);
                }
            }

            return characters.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(PlayerCharacter character, CancellationToken ct = default)
    {
        var path = PathFor(character.Id);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(character, Options), ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var path = PathFor(id);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            File.Delete(path);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string PathFor(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            throw new ArgumentException("Character ID must be a GUID.", nameof(id));

        return Path.Combine(_directory, guid.ToString("N") + ".json");
    }
}