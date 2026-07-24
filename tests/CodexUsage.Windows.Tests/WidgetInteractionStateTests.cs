using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Tests;

public sealed class WidgetInteractionStateTests
{
    [Fact]
    public void DefaultsToRecoverableEditingMode()
    {
        var state = new WidgetInteractionState();

        Assert.True(state.IsEditing);
        Assert.False(state.IsClickThrough);
        Assert.Equal("EDIT MODE", state.DisplayText);
    }

    [Fact]
    public void ToggleSwitchesBetweenEditingAndClickThrough()
    {
        var state = new WidgetInteractionState();

        state.Toggle();

        Assert.False(state.IsEditing);
        Assert.True(state.IsClickThrough);
        Assert.Equal("LOCKED · CLICK-THROUGH", state.DisplayText);

        state.Toggle();

        Assert.True(state.IsEditing);
        Assert.False(state.IsClickThrough);
    }
}
