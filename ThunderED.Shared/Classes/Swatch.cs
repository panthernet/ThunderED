using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ThunderED.Classes
{
    public static class Swatch
    {
        public static async Task Run(Action action, string header = null)
        {
            if(action == null) return;

            var sw = Stopwatch.StartNew();
            try
            {
                action.Invoke();
            }
            finally
            {
                sw.Stop();
                Console.WriteLine($"[SWATCH] {header}: {sw.ElapsedMilliseconds}");
            }

            await Task.CompletedTask;
        }

        public static async Task Run(Func<Task> action, string header = null)
        {
            if (action == null) return;

            var sw = Stopwatch.StartNew();
            try
            {
                await action.Invoke();
            }
            finally
            {
                sw.Stop();
                Console.WriteLine($"[SWATCH] {header}: {sw.ElapsedMilliseconds}");
            }
        }
    }
}
