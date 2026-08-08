using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MikaNetwork
{
    public sealed class MikaSendQueue
    {
        private readonly ConcurrentQueue<ReadOnlyMemory<byte>> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly int _capacity;
        private int _count;
    
        public MikaSendQueue(int capacity = 1024) => _capacity = capacity;
    
        public bool TryWrite(ReadOnlyMemory<byte> data)
        {
            if (Volatile.Read(ref _count) >= _capacity) 
                return false; // 백프레셔
            
            _queue.Enqueue(data);
            Interlocked.Increment(ref _count);
            _signal.Release();                                        // reader 깨움
            return true;
        }
    
        public bool TryRead(out ReadOnlyMemory<byte> data)
        {
            if (_queue.TryDequeue(out data)) 
            {
                Interlocked.Decrement(ref _count); return true;
            }
            return false;                                            // ← 비면 false (버그 수정)
        }
    
        public Task WaitToReadAsync(CancellationToken ct) => _signal.WaitAsync(ct);
    }
}