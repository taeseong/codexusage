using CodexUsage.Desktop.ViewModels;
using System.Runtime.InteropServices;
using static CodexUsage.macOS.MenuBar.ObjectiveC;
using static CodexUsage.macOS.MenuBar.NativeUsagePopoverTokens;

namespace CodexUsage.macOS.MenuBar;

internal sealed class NativeUsagePopover : IDisposable
{
    private const long MinYEdge = 1;
    private static long CenterAlignment => RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 2L : 1L;
    private static long RightAlignment => RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 1L : 2L;

    private readonly nint _popover;
    private readonly nint _root;
    private readonly nint _headerTitle;
    private readonly nint _refreshButton;
    private readonly nint _headerDivider;
    private readonly nint _metricRegion;
    private readonly nint _metricDivider;
    private readonly nint _metricBottomDivider;
    private readonly nint _usedValue;
    private readonly nint _remainingValue;
    private readonly nint _usedSpinner;
    private readonly nint _remainingSpinner;
    private readonly nint _primaryRow;
    private readonly nint _primaryTitle;
    private readonly nint _primaryReset;
    private readonly nint _secondaryRow;
    private readonly nint _secondaryTitle;
    private readonly nint _secondaryValue;
    private readonly nint _failureRegion;
    private readonly nint _failureTitle;
    private readonly nint _failureDetail;
    private readonly nint _footerDivider;
    private readonly nint _footerStatus;
    private bool _hasLayout;
    private bool _showsFailure;
    private bool _showsSecondary;
    private bool _disposed;

    internal NativeUsagePopover(nint target, MenuBarPresentation presentation)
    {
        _popover = Send(Send(Class("NSPopover"), Selector("alloc")), Selector("init"));
        SendVoid(_popover, Selector("setBehavior:"), 1L);
        SendBool(_popover, Selector("setAnimates:"), false);

        var controller = Send(Send(Class("NSViewController"), Selector("alloc")), Selector("init"));
        _root = Send(Send(Class("NSView"), Selector("alloc")), Selector("initWithFrame:"), new Rect(0, 0, Width, InitialHeight));
        SendVoid(controller, Selector("setView:"), _root);
        SendVoid(_popover, Selector("setContentViewController:"), controller);
        SendVoid(controller, Selector("release"));
        SendVoid(_root, Selector("release"));

        _headerTitle = AddLabel(_root, "Codex Usage", TitleFont, true, Gutter, HeaderTitleInitialY, HeaderTitleWidth, TitleLineHeight);
        _refreshButton = AddRefreshButton(_root, target);
        _headerDivider = AddDivider(_root);

        _metricRegion = AddView(_root);
        AddMetric(_metricRegion, "Used", out _usedValue, out _usedSpinner);
        AddMetric(_metricRegion, "Remaining", out _remainingValue, out _remainingSpinner, x: MetricSecondColumnX);
        _metricDivider = AddDivider(_metricRegion, vertical: true);
        _metricBottomDivider = AddDivider(_metricRegion);

        _primaryRow = AddView(_root);
        AddSymbolImage(_primaryRow, "calendar", "Usage limit", PrimaryIconX, PrimaryIconY);
        _primaryTitle = AddLabel(_primaryRow, string.Empty, BodyFont, false, PrimaryTitleX, PrimaryTextY, PrimaryTitleWidth, BodyLineHeight);
        _primaryReset = AddLabel(_primaryRow, string.Empty, BodyFont, false, PrimaryResetX, PrimaryTextY, PrimaryResetWidth, BodyLineHeight, alignment: RightAlignment);

        _secondaryRow = AddView(_root);
        _secondaryTitle = AddLabel(_secondaryRow, string.Empty, CaptionFont, false, Gutter, SecondaryTextY, SecondaryTitleWidth, CaptionLineHeight, secondary: true);
        _secondaryValue = AddLabel(_secondaryRow, string.Empty, CaptionFont, false, SecondaryValueX, SecondaryTextY, SecondaryValueWidth, CaptionLineHeight, alignment: RightAlignment, secondary: true);

        _failureRegion = AddView(_root);
        _failureTitle = AddLabel(_failureRegion, string.Empty, TitleFont, true, Gutter, FailureTitleY, Width - (Gutter * 2), TitleLineHeight, alignment: CenterAlignment, error: true);
        _failureDetail = AddLabel(_failureRegion, string.Empty, BodyFont, false, FailureDetailX, FailureDetailY, Width - (FailureDetailX * 2), FailureDetailHeight, alignment: CenterAlignment, secondary: true, wraps: true);

        _footerDivider = AddDivider(_root);
        _footerStatus = AddLabel(_root, string.Empty, CaptionFont, false, Gutter, FooterStatusY, FooterStatusWidth, CaptionLineHeight, secondary: true);
        AddTextButton(_root, "Details", target, "openUsage:", DetailButtonX, FooterButtonY, FooterButtonWidth);
        AddTextButton(_root, "Quit", target, "quitUsage:", QuitButtonX, FooterButtonY, FooterButtonWidth);

        Update(presentation);
    }

    internal void Toggle(nint anchor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (SendBoolResult(_popover, Selector("isShown")))
        {
            SendVoid(_popover, Selector("performClose:"), 0);
            return;
        }

        Show(anchor);
    }

    internal void Update(MenuBarPresentation presentation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var showsFailure = presentation.ShowsUnavailableState && !presentation.IsLoading;
        var showsSecondary = !showsFailure && presentation.SecondaryLimit is not null;
        if (!_hasLayout || showsFailure != _showsFailure || showsSecondary != _showsSecondary)
        {
            Layout(showsFailure, showsSecondary);
            _hasLayout = true;
            _showsFailure = showsFailure;
            _showsSecondary = showsSecondary;
        }

        SetHidden(_metricRegion, showsFailure);
        SetHidden(_primaryRow, showsFailure);
        SetHidden(_secondaryRow, !showsSecondary);
        SetHidden(_failureRegion, !showsFailure);
        SetHidden(_usedValue, presentation.IsLoading);
        SetHidden(_remainingValue, presentation.IsLoading);
        SetHidden(_usedSpinner, !presentation.IsLoading);
        SetHidden(_remainingSpinner, !presentation.IsLoading);
        SetSpinning(_usedSpinner, presentation.IsLoading);
        SetSpinning(_remainingSpinner, presentation.IsLoading);
        SendBool(_refreshButton, Selector("setEnabled:"), presentation.CanRefresh);

        SetText(_usedValue, presentation.PrimaryLimit.UsedText);
        SetText(_remainingValue, presentation.PrimaryLimit.RemainingText);
        SetText(_primaryTitle, presentation.PrimaryLimit.Title);
        SetText(_primaryReset, presentation.IsLoading ? "Updating…" : presentation.PrimaryLimit.ResetText);
        if (presentation.SecondaryLimit is { } secondary)
        {
            SetText(_secondaryTitle, secondary.Title);
            SetText(_secondaryValue, $"{secondary.UsedText} · {secondary.ResetText}");
        }

        SetText(_failureTitle, presentation.NoticeTitle ?? presentation.PrimaryLimit.AvailabilityText);
        SetText(_failureDetail, presentation.NoticeDetail ?? "Check Codex status, then try again.");
        SetText(_footerStatus, presentation.AccountAndRefreshStatus);
    }

    internal void Reposition(nint anchor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (SendBoolResult(_popover, Selector("isShown")))
        {
            Show(anchor);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SendVoid(_popover, Selector("performClose:"), 0);
        SendVoid(_popover, Selector("release"));
    }

    private void Layout(bool showsFailure, bool showsSecondary)
    {
        var bodyHeight = showsFailure ? FailureBodyHeight : MetricHeight + PrimaryRowHeight + (showsSecondary ? SecondaryRowHeight : 0d);
        var height = HeaderHeight + bodyHeight + FooterHeight;
        SendVoid(_popover, Selector("setContentSize:"), new Size(Width, height));
        SetFrame(_root, 0, 0, Width, height);
        SetFrame(_headerTitle, Gutter, height - HeaderTitleBottomInset, HeaderTitleWidth, TitleLineHeight);
        SetFrame(_refreshButton, Width - HeaderActionInset - MinimumTarget, height - HeaderHeight + FooterButtonY, MinimumTarget, MinimumTarget);
        SetFrame(_headerDivider, 0, height - HeaderHeight, Width, Divider);
        SetFrame(_metricRegion, 0, FooterHeight + PrimaryRowHeight + (showsSecondary ? SecondaryRowHeight : 0d), Width, MetricHeight);
        SetFrame(_metricDivider, MetricColumnWidth, 0, Divider, MetricHeight);
        SetFrame(_metricBottomDivider, 0, 0, Width, Divider);
        SetFrame(_primaryRow, 0, FooterHeight + (showsSecondary ? SecondaryRowHeight : 0d), Width, PrimaryRowHeight);
        SetFrame(_secondaryRow, 0, FooterHeight, Width, SecondaryRowHeight);
        SetFrame(_failureRegion, 0, FooterHeight, Width, bodyHeight);
        SetFrame(_footerDivider, 0, FooterHeight - Divider, Width, Divider);
    }

    private void Show(nint anchor) =>
        SendVoid(
            _popover,
            Selector("showRelativeToRect:ofView:preferredEdge:"),
            SendRect(anchor, Selector("bounds")),
            anchor,
            MinYEdge);

    private static void AddMetric(nint parent, string caption, out nint value, out nint spinner, double x = 0)
    {
        AddLabel(parent, caption, CaptionFont, false, x, MetricCaptionY, MetricColumnWidth, CaptionLineHeight, alignment: CenterAlignment, secondary: true);
        value = AddLabel(parent, "-", MetricFont, true, x, MetricValueY, MetricColumnWidth, MetricLineHeight, alignment: CenterAlignment);
        var spinnerX = x + ((MetricColumnWidth - MetricSpinnerSize) / 2d);
        spinner = Send(Send(Class("NSProgressIndicator"), Selector("alloc")), Selector("initWithFrame:"), new Rect(spinnerX, MetricSpinnerY, MetricSpinnerSize, MetricSpinnerSize));
        SendVoid(spinner, Selector("setStyle:"), 1L);
        SendBool(spinner, Selector("setDisplayedWhenStopped:"), false);
        SendVoid(parent, Selector("addSubview:"), spinner);
        SendVoid(spinner, Selector("release"));
    }

    private static nint AddLabel(nint parent, string text, double size, bool bold, double x, double y, double width, double height, long alignment = 0, bool secondary = false, bool error = false, bool wraps = false)
    {
        var label = Send(Class("NSTextField"), Selector("labelWithString:"), String(text));
        SetFrame(label, x, y, width, height);
        SendVoid(label, Selector("setFont:"), Send(Class("NSFont"), Selector(bold ? "boldSystemFontOfSize:" : "systemFontOfSize:"), size));
        var color = error ? "systemRedColor" : secondary ? "secondaryLabelColor" : "labelColor";
        SendVoid(label, Selector("setTextColor:"), Send(Class("NSColor"), Selector(color)));
        SendVoid(label, Selector("setAlignment:"), alignment);
        SendVoid(label, Selector("setAccessibilityLabel:"), String(text));
        if (wraps)
        {
            SendVoid(label, Selector("setMaximumNumberOfLines:"), 2L);
            SendVoid(Send(label, Selector("cell")), Selector("setLineBreakMode:"), 0L);
        }
        SendVoid(parent, Selector("addSubview:"), label);
        return label;
    }

    private static nint AddView(nint parent)
    {
        var view = Send(Send(Class("NSView"), Selector("alloc")), Selector("initWithFrame:"), new Rect(0, 0, 1, 1));
        SendVoid(parent, Selector("addSubview:"), view);
        SendVoid(view, Selector("release"));
        return view;
    }

    private static nint AddDivider(nint parent, bool vertical = false)
    {
        var divider = Send(Send(Class("NSBox"), Selector("alloc")), Selector("initWithFrame:"), new Rect(0, 0, vertical ? Divider : Width, vertical ? MetricHeight : Divider));
        SendVoid(divider, Selector("setBoxType:"), 2L);
        SendVoid(parent, Selector("addSubview:"), divider);
        SendVoid(divider, Selector("release"));
        return divider;
    }

    private static nint AddRefreshButton(nint parent, nint target)
    {
        var image = Send(Class("NSImage"), Selector("imageWithSystemSymbolName:accessibilityDescription:"), String("arrow.clockwise"), String("Refresh"));
        var button = Send(Class("NSButton"), Selector("buttonWithImage:target:action:"), image, target, Selector("refreshUsage:"));
        SetFrame(button, Width - HeaderActionInset - MinimumTarget, FooterButtonY, MinimumTarget, MinimumTarget);
        SendBool(button, Selector("setBordered:"), false);
        SendVoid(button, Selector("setContentTintColor:"), Send(Class("NSColor"), Selector("labelColor")));
        SendVoid(button, Selector("setToolTip:"), String("Refresh now"));
        SendVoid(button, Selector("setAccessibilityLabel:"), String("Refresh now"));
        SendVoid(parent, Selector("addSubview:"), button);
        return button;
    }

    private static void AddSymbolImage(nint parent, string symbol, string description, double x, double y)
    {
        var image = Send(
            Class("NSImage"),
            Selector("imageWithSystemSymbolName:accessibilityDescription:"),
            String(symbol),
            String(description));
        var imageView = Send(Class("NSImageView"), Selector("imageViewWithImage:"), image);
        SetFrame(imageView, x, y, SymbolSize, SymbolSize);
        SendVoid(imageView, Selector("setAccessibilityLabel:"), String(description));
        SendVoid(parent, Selector("addSubview:"), imageView);
    }

    private static void AddTextButton(nint parent, string title, nint target, string action, double x, double y, double width)
    {
        var button = Send(Class("NSButton"), Selector("buttonWithTitle:target:action:"), String(title), target, Selector(action));
        SetFrame(button, x, y, width, MinimumTarget);
        SendBool(button, Selector("setBordered:"), false);
        SendVoid(parent, Selector("addSubview:"), button);
    }

    private static void SetFrame(nint view, double x, double y, double width, double height) =>
        SendVoid(view, Selector("setFrame:"), new Rect(x, y, width, height));

    private static void SetText(nint label, string text)
    {
        SendVoid(label, Selector("setStringValue:"), String(text));
        SendVoid(label, Selector("setAccessibilityLabel:"), String(text));
    }

    private static void SetHidden(nint view, bool hidden) => SendBool(view, Selector("setHidden:"), hidden);

    private static void SetSpinning(nint spinner, bool spinning) =>
        SendVoid(spinner, Selector(spinning ? "startAnimation:" : "stopAnimation:"), 0);
}
