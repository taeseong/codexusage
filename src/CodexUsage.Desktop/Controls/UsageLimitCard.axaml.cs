using Avalonia;
using Avalonia.Controls;

namespace CodexUsage.Desktop.Controls;

public partial class UsageLimitCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<UsageLimitCard, string>(nameof(Title), "Usage limit");

    public static readonly StyledProperty<string> AvailabilityTextProperty =
        AvaloniaProperty.Register<UsageLimitCard, string>(nameof(AvailabilityText), "Available");

    public static readonly StyledProperty<string> UsedTextProperty =
        AvaloniaProperty.Register<UsageLimitCard, string>(nameof(UsedText), "-");

    public static readonly StyledProperty<string> RemainingTextProperty =
        AvaloniaProperty.Register<UsageLimitCard, string>(nameof(RemainingText), "-");

    public static readonly StyledProperty<string> ResetTextProperty =
        AvaloniaProperty.Register<UsageLimitCard, string>(nameof(ResetText), "Not available");

    public static readonly StyledProperty<double> UsedPercentProperty =
        AvaloniaProperty.Register<UsageLimitCard, double>(nameof(UsedPercent));

    public static readonly StyledProperty<bool> ShowProgressProperty =
        AvaloniaProperty.Register<UsageLimitCard, bool>(nameof(ShowProgress));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<UsageLimitCard, bool>(nameof(IsLoading));

    public UsageLimitCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string AvailabilityText
    {
        get => GetValue(AvailabilityTextProperty);
        set => SetValue(AvailabilityTextProperty, value);
    }

    public string UsedText
    {
        get => GetValue(UsedTextProperty);
        set => SetValue(UsedTextProperty, value);
    }

    public string RemainingText
    {
        get => GetValue(RemainingTextProperty);
        set => SetValue(RemainingTextProperty, value);
    }

    public string ResetText
    {
        get => GetValue(ResetTextProperty);
        set => SetValue(ResetTextProperty, value);
    }

    public double UsedPercent
    {
        get => GetValue(UsedPercentProperty);
        set => SetValue(UsedPercentProperty, value);
    }

    public bool ShowProgress
    {
        get => GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
}
