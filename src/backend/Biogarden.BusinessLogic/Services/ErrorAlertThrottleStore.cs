namespace Biogarden.BusinessLogic.Services;

public sealed class ErrorAlertThrottleStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset?> _nextAttemptByKey = new();

    public bool TryAcquire(string key, DateTimeOffset now)
    {
        lock (_gate)
        {
            foreach (var expired in _nextAttemptByKey.Where(throttleEntry => throttleEntry.Value.HasValue && throttleEntry.Value <= now).Select(throttleEntry => throttleEntry.Key).ToArray())
                _nextAttemptByKey.Remove(expired);
            if (_nextAttemptByKey.ContainsKey(key))
                return false;
            _nextAttemptByKey[key] = null; // In flight: another request must not send the same alert.
            return true;
        }
    }

    public void Complete(string key, DateTimeOffset now, TimeSpan cooldown)
    {
        lock (_gate) _nextAttemptByKey[key] = now + cooldown;
    }
}
