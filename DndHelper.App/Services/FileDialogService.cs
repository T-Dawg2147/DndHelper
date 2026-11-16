using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace DndHelper.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType MapFiles = new("D&D Helper map") { Patterns = ["*.dndmap"] };
    private static readonly FilePickerFileType TextFiles = new("Text file") { Patterns = ["*.txt"] };
    private static readonly FilePickerFileType ImageFiles = new("PNG image") { Patterns = ["*.png"] };

    private static IStorageProvider? Provider
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;

    public async Task<string?> PickOpenAsync(string title)
    {
        if (Provider is not { } provider) return null;

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [MapFiles]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveAsync(string title, string suggestedName, FileKind kind = FileKind.Map)
    {
        if (Provider is not { } provider) return null;

        var (type, extension) = kind switch
        {
            FileKind.Text => (TextFiles, "txt"),
            FileKind.Image => (ImageFiles, "png"),
            _ => (MapFiles, "dndmap")
        };

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [type],
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }
}
