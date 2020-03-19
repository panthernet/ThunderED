using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Restarter
{
    internal class Program
    {
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);

        static async Task Main(string[] args)
        {
            if(args.Length == 0) return;
            await LogHelper.LogInfo($"Restarter run with {args[0]}");
            await Task.Delay(1000);
            try
            {
                var file = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                    $"ThunderED{(IsLinux ? null : ".exe")}");
                await LogHelper.LogInfo($"File: {file}");

                var start = new ProcessStartInfo
                {
                    UseShellExecute = !IsLinux,
                    CreateNoWindow = true,
                    FileName = file,
                    Arguments = args[
                        0], // This just gets some command line arguments for the app i am attempting to launch
                    WorkingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)),
                };

                using var proc = new Process {StartInfo = start};
                proc.Start();
                await LogHelper.LogInfo($"Closing...");

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("", ex);
            }

           // Console.ReadKey();
        }
    }
}
