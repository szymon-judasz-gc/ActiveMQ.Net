using System.Threading;
using System.Threading.Tasks;

namespace ActiveMQ.Net.InternalUtilities
{
    internal static class TaskExtensions
    {
        public static TaskCompletionSource<T> CreateTaskCompletionSource<T>(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => tcs.SetCanceled());
            }

            return tcs;
        }
    }
}