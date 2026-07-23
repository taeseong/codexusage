using System.Xml.Linq;

namespace CodexUsage.Desktop.Tests;

public sealed class DesignTokensTests
{
    [Fact]
    public void ActionButtonStyle_CentersContentInBothAxes()
    {
        var setters = LoadStyleSetters("Button.usage-action");

        Assert.Equal("Center", setters["HorizontalContentAlignment"]);
        Assert.Equal("Center", setters["VerticalContentAlignment"]);
    }

    [Fact]
    public void ActionButtonStyle_UsesQuietSurfacePalette()
    {
        var setters = LoadStyleSetters("Button.usage-action");

        Assert.Equal("{DynamicResource UsageSurfaceSubtleBrush}", setters["Background"]);
        Assert.Equal("{DynamicResource UsageTextPrimaryBrush}", setters["Foreground"]);
        Assert.Equal("{DynamicResource UsageBorderBrush}", setters["BorderBrush"]);
        Assert.Equal("1", setters["BorderThickness"]);
    }

    private static IReadOnlyDictionary<string, string> LoadStyleSetters(string selector)
    {
        var document = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "DesignTokens.axaml"));
        var avalonia = document.Root!.Name.Namespace;
        var actionStyle = document
            .Descendants(avalonia + "Style")
            .Single(element =>
                string.Equals(
                    (string?)element.Attribute("Selector"),
                    selector,
                    StringComparison.Ordinal));
        return actionStyle
            .Elements(avalonia + "Setter")
            .ToDictionary(
                element => (string)element.Attribute("Property")!,
                element => (string)element.Attribute("Value")!,
                StringComparer.Ordinal);
    }
}
