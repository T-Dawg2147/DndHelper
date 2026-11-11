using DndHelper.Core.Characters;
using DndHelper.Core.Rules;
using DndHelper.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DndHelper.Tests.Characters;

public sealed class JsonCharacterStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dndhelper-chars-{Guid.NewGuid():N}");
    private readonly JsonCharacterStore _store;

    public JsonCharacterStoreTests()
        => _store = new JsonCharacterStore(_directory, NullLogger<JsonCharacterStore>.Instance);

    private static PlayerCharacter Character(string name) => new()
    {
        Name = name,
        Species = "Elf",
        ClassName = "Wizard",
        Background = "Sage",
        Level = 4,
        BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Intelligence] = 2, [Ability.Wisdom] = 1 },
        ClassSkills = [Skill.Investigation, Skill.Medicine],
        ArmorClassOverride = 13,
        Notes = "Carries a raven."
    };

    [Fact]
    public async Task SaveThenLoad_RoundTripsEverything()
    {
        var original = Character("Mira");
        original.BaseScores[Ability.Intelligence] = 15;

        await _store.SaveAsync(original);
        var loaded = (await _store.GetAllAsync()).Single();

        loaded.Id.Should().Be(original.Id);
        loaded.Name.Should().Be("Mira");
        loaded.Level.Should().Be(4);
        loaded.BaseScores[Ability.Intelligence].Should().Be(15);
        loaded.BackgroundBonuses.Should().BeEquivalentTo(original.BackgroundBonuses);
        loaded.ClassSkills.Should().Equal(Skill.Investigation, Skill.Medicine);
        loaded.ArmorClassOverride.Should().Be(13);
        loaded.Notes.Should().Be("Carries a raven.");
        loaded.Summary.Should().Be("Level 4 Elf Wizard");
    }

    [Fact]
    public async Task Save_SameId_Overwrites()
    {
        var character = Character("Mira");
        await _store.SaveAsync(character);

        character.Name = "Mira the Wise";
        await _store.SaveAsync(character);

        (await _store.GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be("Mira the Wise");
    }

    [Fact]
    public async Task GetAll_SortsByName()
    {
        await _store.SaveAsync(Character("Zed"));
        await _store.SaveAsync(Character("Abel"));

        (await _store.GetAllAsync()).Select(c => c.Name).Should().Equal("Abel", "Zed");
    }

    [Fact]
    public async Task Delete_RemovesCharacter()
    {
        var character = Character("Mira");
        await _store.SaveAsync(character);

        await _store.DeleteAsync(character.Id);

        (await _store.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_MissingCharacter_IsHarmless()
        => await _store.DeleteAsync(Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NonGuidIds_AreRejected()
    {
        var character = Character("Evil");
        character.Id = @"..\..\evil";

        var save = () => _store.SaveAsync(character);
        await save.Should().ThrowAsync<ArgumentException>();

        var delete = () => _store.DeleteAsync(@"..\..\evil");
        await delete.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CorruptFiles_AreSkipped()
    {
        await _store.SaveAsync(Character("Good"));
        await File.WriteAllTextAsync(Path.Combine(_directory, "broken.json"), "{ not json");

        (await _store.GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be("Good");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}