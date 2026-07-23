using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Desktop.ViewModels;

public sealed record MenuBarLimitPresentation
{
    public required string Title { get; init; }

    public required string UsedText { get; init; }

    public required string RemainingText { get; init; }

    public required string ResetText { get; init; }

    public required string AvailabilityText { get; init; }

    public bool IsAvailable { get; init; }
}

public sealed record MenuBarPresentation
{
    public required string StatusItemTitle { get; init; }

    public required string ToolTip { get; init; }

    public required string ShortTermSummary { get; init; }

    public required string ShortTermReset { get; init; }

    public required string WeeklySummary { get; init; }

    public required string WeeklyReset { get; init; }

    public required string AccountAndRefreshStatus { get; init; }

    public string? NoticeTitle { get; init; }

    public string? NoticeDetail { get; init; }

    public bool NoticeIsWarning { get; init; }

    public required string RefreshActionTitle { get; init; }

    public bool CanRefresh { get; init; }

    public required MenuBarLimitPresentation PrimaryLimit { get; init; }

    public MenuBarLimitPresentation? SecondaryLimit { get; init; }

    public bool IsLoading { get; init; }

    public bool ShowsUnavailableState { get; init; }

    public static MenuBarPresentation From(UsageViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var statusItemTitle = viewModel.ShortTerm.ShowProgress
            ? $"5H {viewModel.ShortTerm.UsedText}"
            : viewModel.Weekly.ShowProgress
                ? viewModel.Weekly.UsedText
                : viewModel.IsBusy || !viewModel.HasRefreshed
                    ? "Codex …"
                    : "Codex !";

        if (viewModel.IsShowingStaleData)
        {
            statusItemTitle += " · Stale";
        }

        var primary = viewModel.ShortTerm.ShowProgress
            ? viewModel.ShortTerm
            : viewModel.Weekly.ShowProgress
                ? viewModel.Weekly
                : viewModel.ShortTerm;
        var secondary = viewModel.ShortTerm.ShowProgress && viewModel.Weekly.ShowProgress
            ? viewModel.Weekly
            : null;

        return new MenuBarPresentation
        {
            StatusItemTitle = statusItemTitle,
            ToolTip = string.IsNullOrEmpty(viewModel.MenuSummary)
                ? $"Codex Usage · {viewModel.StatusTitle}"
                : viewModel.TrayToolTip,
            ShortTermSummary = FormatLimitSummary(viewModel.ShortTerm),
            ShortTermReset = $"5-hour reset · {viewModel.ShortTerm.ResetText}",
            WeeklySummary = FormatLimitSummary(viewModel.Weekly),
            WeeklyReset = $"Weekly reset · {viewModel.Weekly.ResetText}",
            AccountAndRefreshStatus = FormatFooterStatus(viewModel),
            NoticeTitle = viewModel.HasNotice ? viewModel.StatusTitle : null,
            NoticeDetail = viewModel.HasNotice ? viewModel.StatusDetail : null,
            NoticeIsWarning = viewModel.IsWarningNotice,
            RefreshActionTitle = viewModel.IsBusy ? "Refreshing…" : "Refresh now",
            CanRefresh = !viewModel.IsBusy,
            PrimaryLimit = FormatLimit(primary),
            SecondaryLimit = secondary is null ? null : FormatLimit(secondary),
            IsLoading = viewModel.IsBusy || !viewModel.HasRefreshed,
            ShowsUnavailableState = !primary.ShowProgress,
        };
    }

    private static MenuBarLimitPresentation FormatLimit(UsageLimitItemViewModel limit) => new()
    {
        Title = limit.Title,
        UsedText = limit.UsedText,
        RemainingText = limit.RemainingText,
        ResetText = limit.ResetText,
        AvailabilityText = limit.AvailabilityText,
        IsAvailable = limit.ShowProgress,
    };

    private static string FormatLimitSummary(UsageLimitItemViewModel limit) => limit.ShowProgress
        ? $"{limit.Title} {limit.UsedText} used · {limit.RemainingText} remaining"
        : $"{limit.Title} · {limit.AvailabilityText}";

    private static string FormatFooterStatus(UsageViewModel viewModel)
    {
        if (!viewModel.HasRefreshed)
        {
            return "Checking…";
        }

        if (viewModel.HasNotice)
        {
            return viewModel.IsShowingStaleData
                ? $"{viewModel.AccountPlanText} · Stale"
                : viewModel.AccountPlanText;
        }

        const string prefix = "Last updated ";
        var updateTime = viewModel.LastUpdatedText.StartsWith(prefix, StringComparison.Ordinal)
            ? viewModel.LastUpdatedText[prefix.Length..]
            : viewModel.LastUpdatedText;
        if (updateTime.Length > 5)
        {
            updateTime = updateTime[..5];
        }

        return $"{viewModel.AccountPlanText} · Updated {updateTime}";
    }
}
