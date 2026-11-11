using DndHelper.Core.Editing;
using DndHelper.Core.Models;
using DndHelper.Core.Services;
using DndHelper.Data;
using DndHelper.Data.Open5e;
using DndHelper.Tests.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DndHelper.Tests.Editing;

public sealed class CustomCreatureStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dndhelper-custom-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly IContentStore _store;

    public CustomCreatureStoreTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDndHelperData(_dbPath, charactersPath: Path.Combine(Path.GetTempPath(), $"dndhelper-c-{Guid.NewGuid():N}"));
        _provider = services.BuildServiceProvider();
        _store = _provider.GetRequiredService<IContentStore>();
        _store.Initialize();
    }

    private static Creature Custom(string name = "Wolf Rider")
    {
        var draft = CreatureDraft.Blank();
        draft.Name = name;
        draft.TryBuild(out var creature, out _).Should().BeTrue();
        return creature!;
    }

    private static Creature Downloaded() => new()
    {
        Key = "srd_goblin", Name = "Goblin", Type = "Humanoid", SourceKey = "srd-2024", SourceName = "SRD"
    };

    [Fact]
    public async Task SaveCustom_AppearsInSearchAndLoadsBack()
    {
        var creature = Custom();

        await _store.SaveCustomCreatureAsync(creature);

        var found = (await _store.SearchCreaturesAsync(new CreatureQuery(Text: "wolf"))).Single();
        found.Key.Should().Be(creature.Key);
        found.SourceName.Should().Be("Custom");

        var loaded = await _store.GetCreatureAsync(creature.Key);
        loaded!.Name.Should().Be("Wolf Rider");
        loaded.SourceKey.Should().Be(ContentSources.Custom);
    }

    [Fact]
    public async Task SaveCustom_SameKey_UpdatesInsteadOfDuplicating()
    {
        var draft = CreatureDraft.Blank();
        draft.Name = "First Name";
        draft.TryBuild(out var first, out _);
        await _store.SaveCustomCreatureAsync(first!);

        draft.Name = "Second Name";
        draft.TryBuild(out var second, out _);
        await _store.SaveCustomCreatureAsync(second!);

        (await _store.CountCreaturesAsync()).Should().Be(1);
        (await _store.GetCreatureAsync(first!.Key))!.Name.Should().Be("Second Name");
    }

    [Fact]
    public async Task ContentRefresh_KeepsCustomCreatures_AndReplacesDownloadedOnes()
    {
        await _store.ReplaceContentAsync([Downloaded()], []);
        await _store.SaveCustomCreatureAsync(Custom());

        await _store.ReplaceContentAsync([new Creature { Key = "srd_orc", Name = "Orc", Type = "Humanoid", SourceKey = "srd-2024" }], []);

        var names = (await _store.SearchCreaturesAsync(new CreatureQuery())).Select(c => c.Name).ToList();
        names.Should().BeEquivalentTo("Orc", "Wolf Rider");
    }

    [Fact]
    public async Task ContentRefresh_IgnoresCustomCreaturesInTheDownload()
    {
        await _store.SaveCustomCreatureAsync(Custom());
        var impostor = new Creature { Key = "custom_x", Name = "Impostor", SourceKey = ContentSources.Custom };

        await _store.ReplaceContentAsync([impostor], []);

        (await _store.SearchCreaturesAsync(new CreatureQuery())).Select(c => c.Name).Should().Equal("Wolf Rider");
    }

    [Fact]
    public async Task DeleteCustom_RemovesOnlyCustomCreatures()
    {
        await _store.ReplaceContentAsync([Downloaded()], []);
        var creature = Custom();
        await _store.SaveCustomCreatureAsync(creature);

        (await _store.DeleteCustomCreatureAsync("srd_goblin")).Should().BeFalse();
        (await _store.DeleteCustomCreatureAsync(creature.Key)).Should().BeTrue();
        (await _store.DeleteCustomCreatureAsync(creature.Key)).Should().BeFalse();

        (await _store.SearchCreaturesAsync(new CreatureQuery())).Select(c => c.Name).Should().Equal("Goblin");
    }

    [Fact]
    public async Task SaveCustom_RefusesNonCustomCreatures()
    {
        var save = () => _store.SaveCustomCreatureAsync(Downloaded());

        await save.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveCustom_CannotOverwriteDownloadedContentWithTheSameKey()
    {
        await _store.ReplaceContentAsync([Downloaded()], []);
        var clash = new Creature { Key = "srd_goblin", Name = "Hijack", SourceKey = ContentSources.Custom };

        var save = () => _store.SaveCustomCreatureAsync(clash);

        await save.Should().ThrowAsync<InvalidOperationException>();
        (await _store.GetCreatureAsync("srd_goblin"))!.Name.Should().Be("Goblin");
    }

    [Fact]
    public void Mapper_KeepsResistanceTextWithItsNonmagicalQualifier()
    {
        const string json = """
        {
          "key": "x", "name": "Statue",
          "resistances_and_immunities": {
            "damage_resistances": [], "damage_resistances_display": "Cold; Bludgeoning, Piercing, and Slashing from Nonmagical Attacks"
          }
        }
        """;

        var creature = Open5eMapper.ParseCreature(Open5eMapperTests.Parse(json));

        creature.DamageResistances.Should().Equal("Cold", "Bludgeoning, Piercing, and Slashing from Nonmagical Attacks");
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}