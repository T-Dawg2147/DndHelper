using DndHelper.Core.Models;

namespace DndHelper.App.Messages;

/// <summary> Send after local content cache has been refreshed so browsers can reload. </summary>
public sealed record ContentUpdatedMessage;
public sealed record CreatureSelectedMessaged(Creature Creature);
public sealed record SpellSelectedMessage(Spell Spell);