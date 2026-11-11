using System.Text.Json;
using DndHelper.Core.Models;
using DndHelper.Core.Services;
using DndHelper.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DndHelper.Data;

public sealed class ContentStore(IDbContextFactory<ContentDbContext> factory) : IContentStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public void Initialize()
    {
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public async Task<int> CountCreaturesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Creatures.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountSpellsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Spells.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CreatureSummary>> SearchCreaturesAsync(CreatureQuery query, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        IQueryable<CreatureEntity> q = db.Creatures.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{query.Text.Trim()}%";
            q = q.Where(c => EF.Functions.Like(c.Name, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
            q = q.Where(c => c.Type == query.Type);

        if (query.MinChallengeRating is { } min)
            q = q.Where(c => c.ChallengeRating >= min);

        if (query.MaxChallengeRating is { } max)
            q = q.Where(c => c.ChallengeRating <= max);

        var rows = await q.OrderBy(c => c.Name)
            .Select(c => new { c.Key, c.Name, c.Type, c.Size, c.ChallengeRating, c.ChallengeRatingText, c.SourceName })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .Select(r => new CreatureSummary(r.Key, r.Name, r.Type, r.Size, r.ChallengeRating, r.ChallengeRatingText, r.SourceName))
            .ToList();
    }

    public async Task<Creature?> GetCreatureAsync(string key, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var json = await db.Creatures.AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => c.Json)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return json is null ? null : JsonSerializer.Deserialize<Creature>(json, Json);
    }

    public async Task<IReadOnlyList<string>> GetCreatureTypesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Creatures.AsNoTracking()
            .Where(c => c.Type != "")
            .Select(c => c.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpellSummary>> SearchSpellsAsync(SpellQuery query, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        IQueryable<SpellEntity> q = db.Spells.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{query.Text.Trim()}%";
            q = q.Where(s => EF.Functions.Like(s.Name, pattern));
        }

        if (query.Level is { } level)
            q = q.Where(s => s.Level == level);

        if (!string.IsNullOrWhiteSpace(query.School))
            q = q.Where(s => s.School == query.School);

        if (!string.IsNullOrWhiteSpace(query.ClassName))
        {
            var classPattern = $"%|{query.ClassName}|%";
            q = q.Where(s => EF.Functions.Like(s.ClassDelimited, classPattern));
        }

        var rows = await q.OrderBy(s => s.Level).ThenBy(s => s.Name)
            .Select(s => new { s.Key, s.Name, s.Level, s.School, s.Concentration, s.Ritual, s.SourceName })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .Select(r => new SpellSummary(r.Key, r.Name, r.Level, r.School, r.Concentration, r.Ritual, r.SourceName))
            .ToList();
    }

    public async Task<Spell?> GetSpellAsync(string key, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var json = await db.Spells.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Json)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return json is null ? null : JsonSerializer.Deserialize<Spell>(json, Json);
    }

    public async Task<IReadOnlyList<string>> GetSpellSchoolsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Spells.AsNoTracking()
            .Where(s => s.School != "")
            .Select(s => s.School)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetSpellClassesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var delimited = await db.Spells.AsNoTracking()
            .Select(s => s.ClassDelimited)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        return delimited
            .SelectMany(d => d.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    public async Task ReplaceContentAsync(
        IReadOnlyCollection<Creature> creatures,
        IReadOnlyCollection<Spell> spells,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        await db.Creatures.Where(c => c.SourceKey != ContentSources.Custom).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.Spells.ExecuteDeleteAsync(ct).ConfigureAwait(false);

        db.Creatures.AddRange(creatures.Where(c => c.SourceKey != ContentSources.Custom).Select(c => new CreatureEntity
        {
            Key = c.Key,
            Name = c.Name,
            Type = c.Type,
            Size = c.Size,
            ChallengeRating = c.ChallengeRating,
            ChallengeRatingText = c.ChallengeRatingText,
            SourceKey = c.SourceKey,
            SourceName = c.SourceName,
            Json = JsonSerializer.Serialize(c, Json)
        }));

        db.Spells.AddRange(spells.Select(s => new SpellEntity
        {
            Key = s.Key,
            Name = s.Name,
            Level = s.Level,
            School = s.School,
            ClassDelimited = "|" + string.Join('|', s.Classes) + "|",
            Concentration = s.Concentration,
            Ritual = s.Ritual,
            SourceKey = s.SourceKey,
            SourceName = s.SourceName,
            Json = JsonSerializer.Serialize(s, Json)
        }));

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }
    
    public async Task SaveCustomCreatureAsync(Creature creature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(creature.Key))
            throw new ArgumentException("A creature needs a key to be saved.", nameof(creature));

        if (creature.SourceKey != ContentSources.Custom)
            throw new ArgumentException("Only custom creatures can be saved.", nameof(creature));

        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = await db.Creatures.FindAsync([creature.Key], ct).ConfigureAwait(false);

        // Never overwrite a downloaded creature, even if the key somehow matches.
        if (entity is not null && entity.SourceKey != ContentSources.Custom)
            throw new InvalidOperationException($"'{creature.Key}' belongs to downloaded content and cannot be overwritten.");

        if (entity is null)
        {
            entity = new CreatureEntity { Key = creature.Key };
            db.Creatures.Add(entity);
        }

        entity.Name = creature.Name;
        entity.Type = creature.Type;
        entity.Size = creature.Size;
        entity.ChallengeRating = creature.ChallengeRating;
        entity.ChallengeRatingText = creature.ChallengeRatingText;
        entity.SourceKey = ContentSources.Custom;
        entity.SourceName = creature.SourceName;
        entity.Json = JsonSerializer.Serialize(creature, Json);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteCustomCreatureAsync(string key, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var deleted = await db.Creatures
            .Where(c => c.Key == key && c.SourceKey == ContentSources.Custom)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return deleted > 0;
    }
}