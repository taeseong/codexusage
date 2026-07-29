using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace CodexUsage.Windows.Recovery;

internal sealed class WindowsRefreshRecoveryService : IDisposable
{
    private readonly Action _requestRefresh;
    private readonly Action _subscribeNetwork;
    private readonly Action _unsubscribeNetwork;
    private readonly Action _subscribePower;
    private readonly Action _unsubscribePower;
    private bool _started;
    private bool _disposed;

    public WindowsRefreshRecoveryService(
        Action requestRefresh,
        Action? subscribeNetwork = null,
        Action? unsubscribeNetwork = null,
        Action? subscribePower = null,
        Action? unsubscribePower = null)
    {
        ArgumentNullException.ThrowIfNull(requestRefresh);
        _requestRefresh = requestRefresh;
        _subscribeNetwork = subscribeNetwork ??
            (() => NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged);
        _unsubscribeNetwork = unsubscribeNetwork ??
            (() => NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged);
        _subscribePower = subscribePower ??
            (() => SystemEvents.PowerModeChanged += OnPowerModeChanged);
        _unsubscribePower = unsubscribePower ??
            (() => SystemEvents.PowerModeChanged -= OnPowerModeChanged);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _subscribeNetwork();
        try
        {
            _subscribePower();
            _started = true;
        }
        catch
        {
            try
            {
                _unsubscribeNetwork();
            }
            catch
            {
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_started)
        {
            return;
        }

        try
        {
            _unsubscribeNetwork();
        }
        catch
        {
        }

        try
        {
            _unsubscribePower();
        }
        catch
        {
        }

        _started = false;
    }

    internal void NotifyNetworkAvailabilityChanged(bool isAvailable)
    {
        if (isAvailable)
        {
            _requestRefresh();
        }
    }

    internal void NotifyPowerModeChanged(PowerModes mode)
    {
        if (mode is PowerModes.Resume)
        {
            _requestRefresh();
        }
    }

    private void OnNetworkAvailabilityChanged(
        object? sender,
        NetworkAvailabilityEventArgs eventArgs) =>
        NotifyNetworkAvailabilityChanged(eventArgs.IsAvailable);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs eventArgs) =>
        NotifyPowerModeChanged(eventArgs.Mode);
}
