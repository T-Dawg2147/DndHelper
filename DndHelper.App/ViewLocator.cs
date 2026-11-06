using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DndHelper.App;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewName = param.GetType().FullName!
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(viewName);
        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found: " + viewName };
    }

    public bool Match(object? data)
        => data?.GetType().Namespace?.StartsWith("DndHelper.App.ViewModels", StringComparison.Ordinal) == true;
}