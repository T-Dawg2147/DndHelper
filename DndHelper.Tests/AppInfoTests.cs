using DndHelper.Core;
using FluentAssertions;

namespace DndHelper.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsNotEmpty()
    {
        AppInfo.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RulesEdition_Targets2024()
    {
        AppInfo.RulesEdition.Should().Contain("2024");
    }
}