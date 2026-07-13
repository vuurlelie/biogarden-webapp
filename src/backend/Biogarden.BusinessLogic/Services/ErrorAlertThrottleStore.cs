using System.Collections.Concurrent;

namespace Biogarden.BusinessLogic.Services;

public sealed class ErrorAlertThrottleStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _sentAtByKey = new();

    public bool TryAcquire(
        string key,
        DateTimeOffset now,
        TimeSpan throttleWindow)
    {
        Cleanup(now, throttleWindow);

        while (true)
        {
            if (!_sentAtByKey.TryGetValue(key, out var previousSentAt))
            {
                if (_sentAtByKey.TryAdd(key, now))
                {
                    return true;
                }

                continue;
            }

            if (now - previousSentAt < throttleWindow)
            {
                return false;
            }

            if (_sentAtByKey.TryUpdate(key, now, previousSentAt))
            {
                return true;
            }
        }
    }

    private void Cleanup(DateTimeOffset now, TimeSpan throttleWindow)
    {
        foreach (var item in _sentAtByKey)
        {
            if (now - item.Value >= throttleWindow)
            {
                _sentAtByKey.TryRemove(item);
            }
        }
    }
}
