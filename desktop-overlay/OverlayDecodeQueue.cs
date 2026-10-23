using System;
using System.Threading;
using System.Threading.Tasks;

namespace FishSocialOverlay
{
    // At most two decoders and a bounded number of admitted jobs. No WPF objects or
    // cache dictionaries are mutated here; callers publish frozen results on UI.
    internal sealed class OverlayDecodeQueue
    {
        readonly SemaphoreSlim _workers;
        readonly int _capacity;
        int _pending;
        public int Pending => Volatile.Read(ref _pending);
        public OverlayDecodeQueue(int workers = 2, int capacity = 256)
        {
            if (workers < 1 || capacity < workers) throw new ArgumentOutOfRangeException(nameof(capacity));
            _workers = new SemaphoreSlim(workers); _capacity = capacity;
        }
        public Task<T> TryRun<T>(Func<T> decode, CancellationToken token = default) where T : class
        {
            if (Interlocked.Increment(ref _pending) > _capacity)
            { Interlocked.Decrement(ref _pending); return null; }
            return Run(decode, token);
        }
        async Task<T> Run<T>(Func<T> decode, CancellationToken token) where T : class
        {
            var entered = false;
            try
            {
                await _workers.WaitAsync(token).ConfigureAwait(false);
                entered = true;
                return await Task.Run(decode, token).ConfigureAwait(false);
            }
            finally
            {
                if (entered) _workers.Release();
                Interlocked.Decrement(ref _pending);
            }
        }
    }
}
