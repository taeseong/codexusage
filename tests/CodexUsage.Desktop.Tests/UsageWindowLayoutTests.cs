using System.Xml.Linq;

namespace CodexUsage.Desktop.Tests;

public sealed class UsageWindowLayoutTests
{
    [Fact]
    public void DefaultSize_FitsTheDetailedContentWithoutExcessVerticalSpace()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;

        Assert.Equal("420", (string?)window.Attribute("Width"));
        Assert.Equal("540", (string?)window.Attribute("Height"));
        Assert.Equal("380", (string?)window.Attribute("MinWidth"));
        Assert.Equal("520", (string?)window.Attribute("MinHeight"));
    }

    [Fact]
    public void UsageLimitCard_PreservesMetricLayoutWhileRefreshing()
    {
        var card = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageLimitCard.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var metrics = card
            .Descendants(avalonia + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "Metrics");

        Assert.Null(metrics.Attribute("IsVisible"));

        var progressIndicators = card
            .Descendants(avalonia + "ProgressBar")
            .ToArray();

        Assert.Equal(2, progressIndicators.Length);
        Assert.All(
            progressIndicators,
            indicator => Assert.Null(indicator.Attribute("IsVisible")));
        Assert.Contains(
            progressIndicators,
            indicator => (string?)indicator.Attribute("Classes.visible")
                == "{Binding ShowProgress, ElementName=Root}");
        Assert.Contains(
            progressIndicators,
            indicator => (string?)indicator.Attribute("Classes.busy")
                == "{Binding IsLoading, ElementName=Root}");
    }

    [Fact]
    public void UsageSurfaces_UseEnglishLabels()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        var card = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageLimitCard.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";

        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Refresh");

        var captions = card
            .Descendants(avalonia + "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .Where(static text => text is not null)
            .ToArray();

        Assert.Contains("Used", captions);
        Assert.Contains("Remaining", captions);
        Assert.Contains("Reset", captions);
    }

    [Fact]
    public void LoadingIndicator_RemainsMeasuredAcrossRefreshStates()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";

        var loadingIndicator = window
            .Descendants(avalonia + "ProgressBar")
            .Single();

        Assert.Null(loadingIndicator.Attribute("IsVisible"));
        Assert.Equal(
            "usage-window-loading",
            (string?)loadingIndicator.Attribute("Classes"));
        Assert.Equal(
            "{Binding IsBusy}",
            (string?)loadingIndicator.Attribute("Classes.busy"));
    }
}
