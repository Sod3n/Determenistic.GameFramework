using System;

namespace Deterministic.GameFramework.Client.Utils;

/// <summary>
/// A simple lock mechanism that can be locked and unlocked multiple times.
/// Useful for temporarily pausing action processing during animations or transitions.
/// </summary>
public class Locker
{
    private int _lockCount;

    public bool IsLocked => _lockCount > 0;

    public IDisposable Lock()
    {
        _lockCount++;
        return new LockerDisposable(this);
    }

    private void Unlock()
    {
        if (_lockCount > 0)
            _lockCount--;
    }

    private class LockerDisposable : IDisposable
    {
        private readonly Locker _locker;
        private bool _disposed;

        public LockerDisposable(Locker locker)
        {
            _locker = locker;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _locker.Unlock();
        }
    }
}
