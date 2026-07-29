namespace CodexUsage.Windows.Settings;

internal sealed class WindowsSettingsWriteGate
{
    public WindowsSettingsWriteGate(WindowsSettingsRecoveryStatus recoveryStatus)
    {
        IsAutomaticWritePaused =
            recoveryStatus is
                WindowsSettingsRecoveryStatus.ReadFailed or
                WindowsSettingsRecoveryStatus.CorruptFilePreservationFailed;
    }

    public bool IsAutomaticWritePaused { get; private set; }

    public bool CanApplyAutomaticChange => !IsAutomaticWritePaused;

    public bool CanWrite(bool allowRecoveryOverwrite) =>
        !IsAutomaticWritePaused || allowRecoveryOverwrite;

    public void OnWriteSucceeded(bool allowRecoveryOverwrite)
    {
        if (allowRecoveryOverwrite)
        {
            IsAutomaticWritePaused = false;
        }
    }
}
