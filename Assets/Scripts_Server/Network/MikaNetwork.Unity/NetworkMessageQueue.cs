#if UNITY_5_3_OR_NEWER

using System;
using System.Collections.Concurrent;
using Utils;

public class NetworkMessageQueue : Singleton<NetworkMessageQueue>
{
    private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    public void Push(Action job) => _queue.Enqueue(job);
    public void Flush(int maxPerFrame = 200)
    {
        int n = 0;
        while (n++ < maxPerFrame && _queue.TryDequeue(out var job))
        {
            try { job.Invoke(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }

}

#endif