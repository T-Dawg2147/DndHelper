using DndHelper.Core.Characters;
using DndHelper.Core.Models;

namespace DndHelper.App.Messages;

/// <summary>Sent after the local content cache has been refreshed so browsers can reload.</summary>
public sealed record ContentUpdatedMessage;

public sealed record CreatureSelectedMessage(Creature Creature);

public sealed record SpellSelectedMessage(Spell Spell);

/// <summary>A saved character was chosen in the character list; the builder loads it for editing.</summary>
public sealed record CharacterSelectedMessage(PlayerCharacter Character);

/// <summary>The user asked for a blank character.</summary>
public sealed record NewCharacterMessage;

/// <summary>Characters were saved or deleted, so the list should reload.</summary>
public sealed record CharactersChangedMessage(string? SelectId = null);

/// <summary>Open a creature in the Creature Editor. Downloaded creatures open as a copy; custom ones are edited in place.</summary>
public sealed record EditCreatureMessage(Creature Creature);