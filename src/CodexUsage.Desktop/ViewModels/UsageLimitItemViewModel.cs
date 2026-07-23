using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CodexUsage.Core.Usage;

namespace CodexUsage.Desktop.ViewModels;

public sealed class UsageLimitItemViewModel(string title) : ObservableObject
{
    private string _availabilityText = "Checking";
    private string _usedText = "-";
    private string _remainingText = "-";
    private string _resetText = "Not available";
    private double _usedPercent;
    private bool _showProgress;

    public string Title { get; } = title;

    public string AvailabilityText
    {
        get => _availabilityText;
        private set => SetProperty(ref _availabilityText, value);
    }

    public string UsedText
    {
        get => _usedText;
        private set => SetProperty(ref _usedText, value);
    }

    public string RemainingText
    {
        get => _remainingText;
        private set => SetProperty(ref _remainingText, value);
    }

    public string ResetText
    {
        get => _resetText;
        private set => SetProperty(ref _resetText, value);
    }

    public double UsedPercent
    {
        get => _usedPercent;
        private set => SetProperty(ref _usedPercent, value);
    }

    public bool ShowProgress
    {
        get => _showProgress;
        private set => SetProperty(ref _showProgress, value);
    }

    public void Update(UsageLimit? limit, DateTimeOffset now)
    {
        if (limit is null)
        {
            MarkUnavailable("Not reported", "Not available");
            return;
        }

        AvailabilityText = "Available";
        UsedText = FormatPercent(limit.UsedPercent);
        RemainingText = FormatPercent(limit.RemainingPercent);
        ResetText = FormatReset(limit.TimeUntilReset(now));
        UsedPercent = limit.UsedPercent;
        ShowProgress = true;
    }

    public void MarkUnavailable(string availabilityText, string resetText)
    {
        AvailabilityText = availabilityText;
        UsedText = "-";
        RemainingText = "-";
        ResetText = resetText;
        UsedPercent = 0d;
        ShowProgress = false;
    }

    private static string FormatPercent(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value, MidpointRounding.AwayFromZero):0}%");

    private static string FormatReset(TimeSpan? remaining)
    {
        if (remaining is null)
        {
            return "Not available";
        }

        var value = remaining.Value;
        if (value.Days > 0)
        {
            return $"in {value.Days}d {value.Hours}h";
        }

        if (value.Hours > 0)
        {
            return $"in {value.Hours}h {value.Minutes}m";
        }

        return $"in {Math.Max(1, value.Minutes)}m";
    }
}
