using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AtopPlugin.Logic;

public class BatchCollectingQueue<T> : ConcurrentQueue<T>
{

    private readonly ConcurrentQueue<T> _delegate = new();
    private readonly object _delegateLock = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public List<T> DequeueAll()
    {
        lock (_delegateLock)
        {
            var resultList = new List<T>();
            while (!_delegate.IsEmpty)
            {
                _delegate.TryDequeue(out var dequeued);
                resultList.Add(dequeued);
            }

            return resultList;
        }
    } 
}