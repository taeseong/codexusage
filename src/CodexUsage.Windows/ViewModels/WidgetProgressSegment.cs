namespace CodexUsage.Windows.ViewModels;

internal sealed record WidgetProgressSegment(
    bool IsFilled,
    bool IsWarning,
    bool IsCritical);
