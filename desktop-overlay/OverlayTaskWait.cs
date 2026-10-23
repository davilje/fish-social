using System.Threading;
using System.Threading.Tasks;

namespace FishSocialOverlay
{
    internal static class OverlayTaskWait
    {
        // A caller can leave a shared request without canceling other actors' waiters.
        public static async Task<T> WithCancellation<T>(Task<T> task, CancellationToken token)
        {
            if (!token.CanBeCanceled) return await task;
            token.ThrowIfCancellationRequested();
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => canceled.TrySetResult(true)))
            {
                await Task.WhenAny(task, canceled.Task);
                token.ThrowIfCancellationRequested();
                return await task;
            }
        }
    }
}
