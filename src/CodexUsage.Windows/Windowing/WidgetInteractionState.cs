using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodexUsage.Windows.Windowing;

internal sealed class WidgetInteractionState : INotifyPropertyChanged
{
    private WidgetInteractionMode _mode = WidgetInteractionMode.Editing;

    public event PropertyChangedEventHandler? PropertyChanged;

    public WidgetInteractionMode Mode
    {
        get => _mode;
        private set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsClickThrough));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public bool IsEditing => Mode is WidgetInteractionMode.Editing;

    public bool IsClickThrough => Mode is WidgetInteractionMode.Locked;

    public string DisplayText => IsEditing ? "EDIT MODE" : "LOCKED · CLICK-THROUGH";

    public void EnterEditingMode() => Mode = WidgetInteractionMode.Editing;

    public void EnterLockedMode() => Mode = WidgetInteractionMode.Locked;

    public void Toggle() => Mode = IsEditing
        ? WidgetInteractionMode.Locked
        : WidgetInteractionMode.Editing;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
