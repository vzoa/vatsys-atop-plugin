using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AtopPlugin.Logic;

public class BatchCollectingQueue<T> : ConcurrentQueue<T>
{
    
    private readonly object _delegateLock = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public List<T> DequeueAll()
    {
        lock (_delegateLock)
        {
            var resultList = new List<T>();
            while (!IsEmpty)
            {
                TryDequeue(out var dequeued);
                resultList.Add(dequeued);
            }

            return resultList;
        }
    } 
}