using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Encounters;
using DndHelper.Core.Rules;

namespace DndHelper.App.Services;

/// <summary>
/// The one running encounter, shared by the tracker, the map and the spell resolver.
/// Anything that changes the encounter calls <see cref="NotifyChanged"/> so every panel refreshes.
/// </summary>
public sealed class EncounterSession
{
    public EncounterSession()
    {
        Encounter = new Encounter(new SystemRandomSource());
        Encounter.Logged += entry => WeakReferenceMessenger.Default.Send(new BattleLogMessage(entry.ToString()));
    }

    public Encounter Encounter { get; }

    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
