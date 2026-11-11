using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DndHelper.App;

/// <summary>
/// Maps a ViewModel to its View by naming convention:
/// DndHelper.App.ViewModels.Panels.FooViewModel -> DndHelper.App.Views.Panels.FooView.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewName = param.GetType().FullName!
            .Replace(".ViewModels.", ".View.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(viewName);
        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found: " + viewName };
    }

    public bool Match(object? data)
        => data?.GetType().Namespace?.StartsWith("DndHelper.App.ViewModels", StringComparison.Ordinal) == true;
}