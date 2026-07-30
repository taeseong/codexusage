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
        Assert.Equal("660", (string?)window.Attribute("Height"));
        Assert.Equal("380", (string?)window.Attribute("MinWidth"));
        Assert.Equal("620", (string?)window.Attribute("MinHeight"));
        Assert.Contains("windows-surface", (string?)window.Attribute("Classes"));
        Assert.Contains("windows-details", (string?)window.Attribute("Classes"));
        Assert.Equal(
            2,
            window.Descendants()
                .Count(element => ((string?)element.Attribute("Classes"))?.Contains(
                    "usage-details-content",
                    StringComparison.Ordinal) is true));
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
            .Single(element => (string?)element.Attribute("Classes") == "usage-window-loading");

        Assert.Null(loadingIndicator.Attribute("IsVisible"));
        Assert.Equal(
            "usage-window-loading",
            (string?)loadingIndicator.Attribute("Classes"));
        Assert.Equal(
            "{Binding IsBusy}",
            (string?)loadingIndicator.Attribute("Classes.busy"));
    }

    [Fact]
    public void History_UsesMonthlyGroupsWithWeeklyRows()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";

        Assert.Contains(
            window.Descendants(avalonia + "ItemsControl"),
            element => (string?)element.Attribute("ItemsSource") == "{Binding SelectedMonthGroup.Entries}");
        Assert.Contains(
            window.Descendants(avalonia + "ComboBox"),
            element =>
                (string?)element.Attribute("ItemsSource") == "{Binding MonthlyGroups}" &&
                (string?)element.Attribute("SelectedItem") == "{Binding SelectedMonthGroup, Mode=TwoWay}");
        Assert.Contains(
            window.Descendants(avalonia + "TextBlock"),
            element => (string?)element.Attribute("Text") == "{Binding DateRangeText}");
        Assert.Contains(
            window.Descendants(avalonia + "ComboBox"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "History observation filter" &&
                       (string?)element.Attribute("SelectedIndex") == "{Binding ObservationFilterIndex}");
    }

    [Fact]
    public void DetailWindow_PersistsNamedTabsAndProvidesHistoryRecovery()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Contains(
            window.Descendants(avalonia + "TabControl"),
            element => (string?)element.Attribute(x + "Name") == "DetailsTabs");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Retry history");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "‹ Older");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Newer ›");
    }
    [Fact]
    public void History_ShowsComparableMetricsInMutuallyExclusiveStates()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";

        var metrics = window
            .Descendants(avalonia + "TextBlock")
            .Where(element => (string?)element.Attribute("Text") == "{Binding MetricsText}")
            .ToArray();

        Assert.Equal(2, metrics.Length);
        Assert.Contains(
            metrics,
            element => (string?)element.Attribute("IsVisible")
                == "{Binding HasInsufficientComparableHistory}");
        Assert.Contains(
            metrics,
            element => (string?)element.Parent?.Attribute("IsVisible")
                == "{Binding HasComparableHistory}");
    }

    [Fact]
    public void DetailWindow_ExposesRecoveryAndAccessibleControlNames()
    {
        var window = XDocument
            .Load(Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml"))
            .Root!;
        XNamespace avalonia = "https://github.com/avaloniaui";

        Assert.Equal(
            "CodexUsage details",
            (string?)window.Attribute("AutomationProperties.Name"));
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element =>
                (string?)element.Attribute("Content") == "{Binding RecoveryActionText}" &&
                (string?)element.Attribute("Command") == "{Binding RecoveryActionCommand}" &&
                (string?)element.Attribute("IsVisible") == "{Binding HasRecoveryAction}");
        Assert.Contains(
            window.Descendants(avalonia + "TabItem"),
            element => (string?)element.Attribute("AutomationProperties.Name")
                == "Current usage");
        Assert.Contains(
            window.Descendants(avalonia + "TabItem"),
            element => (string?)element.Attribute("AutomationProperties.Name")
                == "Weekly usage history");
        Assert.Contains(
            window.Descendants(avalonia + "Border"),
            element => (string?)element.Attribute("AutomationProperties.Name")
                == "{Binding AccessibilitySummaryText}");
        Assert.Contains(
            window.Descendants(avalonia + "Border"),
            element => (string?)element.Attribute("AutomationProperties.LiveSetting") == "Polite");
        Assert.Contains(
            window.Descendants(avalonia + "TextBlock"),
            element => (string?)element.Attribute("Text")
                == "{Binding ObservationSummaryText}");
    }

    [Fact]
    public void DetailWindow_ProvidesKeyboardShortcutsForRefreshAndTabs()
    {
        var source = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "UsageWindow.axaml.cs"));

        Assert.Contains("case Key.R", source, StringComparison.Ordinal);
        Assert.Contains("case Key.D1 or Key.NumPad1", source, StringComparison.Ordinal);
        Assert.Contains("case Key.D2 or Key.NumPad2", source, StringComparison.Ordinal);
        Assert.Contains("RefreshCommand.Execute(null)", source, StringComparison.Ordinal);
    }
}
