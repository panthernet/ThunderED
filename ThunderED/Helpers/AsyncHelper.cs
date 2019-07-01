using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ThunderED.Helpers
{
    internal static class AsyncHelper
    {
        public static ThreadPoolRedirector RedirectToThreadPool() =>
            new ThreadPoolRedirector();
    }

    public struct ThreadPoolRedirector : INotifyCompletion
    {
        // awaiter и awaitable в одном флаконе
        public ThreadPoolRedirector GetAwaiter() => this;

        // true означает выполнять продолжение немедленно 
        public bool IsCompleted => Thread.CurrentThread.IsThreadPoolThread;

        public void OnCompleted(Action continuation) =>
            ThreadPool.QueueUserWorkItem(o => continuation());

        public void GetResult() { }
    }
}
