namespace DndHelper.App.Services;

public enum FileKind
{
    Map,
    Text,
    Image
}

/// <summary>Lets view models ask the user for a file without touching any UI types.</summary>
public interface IFileDialogService
{
    /// <summary>Returns the chosen map file path, or null if the user cancelled.</summary>
    Task<string?> PickOpenAsync(string title);

    /// <summary>Returns the path to save to, or null if the user cancelled.</summary>
    Task<string?> PickSaveAsync(string title, string suggestedName, FileKind kind = FileKind.Map);
}