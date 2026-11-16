using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DndHelper.App.Services;
using DndHelper.App.ViewModels.Panels;
using Microsoft.Extensions.DependencyInjection;

namespace DndHelper.App.View.Panels;

public partial class MapPanelView : UserControl
{
    public MapPanelView() => InitializeComponent();

    /// <summary>Saves what the map currently shows (zoom, tokens and area effects included) as a PNG.</summary>
    private async void ExportImage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapPanelViewModel vm) return;

        var width = (int)MapControl.Bounds.Width;
        var height = (int)MapControl.Bounds.Height;
        if (width < 10 || height < 10) return;

        try
        {
            var dialogs = App.Services.GetRequiredService<IFileDialogService>();
            var path = await dialogs.PickSaveAsync("Export map image", vm.MapName, FileKind.Image);
            if (path is null) return;

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(MapControl);
            bitmap.Save(path, new PngBitmapEncoderOptions());
            vm.StatusText = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            vm.StatusText = $"Could not export the image: {ex.Message}";
        }
    }
}
