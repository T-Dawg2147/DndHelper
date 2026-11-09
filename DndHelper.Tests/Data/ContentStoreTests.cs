using DndHelper.Core.Models;
using DndHelper.Core.Services;
using DndHelper.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DndHelper.Tests.Data;

public sealed class ContentStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dndhelper-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;
    private readonly IContentStore _store;

    public ContentStoreTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDndHelperData(_dbPath);
        _provider = services.BuildServiceProvider();
        _store = _provider.GetRequiredService<IContentStore>();
        _store.Initialize();
    }

    private static Creature Goblin() => Open5eMapperTestsHelper.Goblin();

    private static Spell Spell(string key, string name, int level, string school, params string[] classes) => new()
    {
        Key = key, Name = name, Level = level, School = school, Classes = [.. classes], Concentration = level == 1
    };

    [Fact]
    public async Task RoundTrip_PersistsFullCreature()
    {
        await _store.ReplaceContentAsync([Goblin()], []);

        var loaded = await _store.GetCreatureAsync("srd_goblin");

        loaded.Should().NotBeNull();
        loaded!.Actions.Should().HaveCount(3);
        loaded.AbilityScores["dexterity"].Should().Be(14);
        loaded.ActionGroups.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchCreatures_FiltersByTextTypeAndCr()
    {
        var goblin = Goblin();
        var dragon = new Creature { Key = "dragon", Name = "Red Dragon", Type = "Dragon", ChallengeRating = 17, ChallengeRatingText = "17" };
        await _store.ReplaceContentAsync([goblin, dragon], []);

        (await _store.SearchCreaturesAsync(new CreatureQuery(Text: "gob"))).Select(c => c.Name).Should().Equal("Goblin");
        (await _store.SearchCreaturesAsync(new CreatureQuery(Type: "Dragon"))).Select(c => c.Name).Should().Equal("Red Dragon");
        (await _store.SearchCreaturesAsync(new CreatureQuery(MinChallengeRating: 1))).Select(c => c.Name).Should().Equal("Red Dragon");
        (await _store.SearchCreaturesAsync(new CreatureQuery(MaxChallengeRating: 1))).Select(c => c.Name).Should().Equal("Goblin");
        (await _store.GetCreatureTypesAsync()).Should().Equal("Dragon", "Humanoid");
    }

    [Fact]
    public async Task SearchSpells_FiltersByLevelSchoolAndClass()
    {
        await _store.ReplaceContentAsync([],
        [
            Spell("a", "Magic Missile", 1, "Evocation", "Wizard", "Sorcerer"),
            Spell("b", "Cure Wounds", 1, "Abjuration", "Cleric"),
            Spell("c", "Fireball", 3, "Evocation", "Wizard")
        ]);

        (await _store.SearchSpellsAsync(new SpellQuery(Level: 1))).Select(s => s.Name).Should().Equal("Cure Wounds", "Magic Missile");
        (await _store.SearchSpellsAsync(new SpellQuery(School: "Evocation"))).Select(s => s.Name).Should().Equal("Magic Missile", "Fireball");
        (await _store.SearchSpellsAsync(new SpellQuery(ClassName: "Cleric"))).Select(s => s.Name).Should().Equal("Cure Wounds");
        (await _store.GetSpellClassesAsync()).Should().Equal("Cleric", "Sorcerer", "Wizard");
        (await _store.GetSpellSchoolsAsync()).Should().Equal("Abjuration", "Evocation");
    }

    [Fact]
    public async Task ReplaceContent_ReplacesExistingRows()
    {
        await _store.ReplaceContentAsync([Goblin()], [Spell("a", "Fireball", 3, "Evocation")]);
        await _store.ReplaceContentAsync([], [Spell("b", "Shield", 1, "Abjuration")]);

        (await _store.CountCreaturesAsync()).Should().Be(0);
        (await _store.CountSpellsAsync()).Should().Be(1);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}

internal static class Open5eMapperTestsHelper
{
    public static Creature Goblin()
        => DndHelper.Data.Open5e.Open5eMapper.ParseCreature(Open5eMapperTests.Parse(Open5eMapperTests.GoblinJson));
}