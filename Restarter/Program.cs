using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace Restarter
{
    internal class Program
    {
        private static volatile bool _active;
        private static bool RunAsService;
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
        private static Process _process;
        private static Timer _restartTimer;
        private static NamedPipeServerStream pipe;

        static async Task Main(string[] args)
        {
            if (args.Length > 0)
                if (!bool.TryParse(args[0], out RunAsService))
                    RunAsService = false;
            LogHelper.SetService(RunAsService);

            _restartTimer = new Timer(async state =>
            {
                try
                {
                    if (pipe == null)
                    {
                        pipe = pipe = new NamedPipeServerStream("ThunderED.Restart.Pipe", PipeDirection.Out, 1);
                        await pipe.WaitForConnectionAsync();
                        await LogHelper.LogInfo("Pipe connected");
                    }
                }
                catch
                {
                    pipe?.Dispose();
                    pipe = null;
                }

            }, null, 0, (long)500);

            try
            {
                _active = true;
                while (_active)
                {
                    if (_process == null)
                    {
                        await StartBot();
                       // await pipe.WaitForConnectionAsync();
                    }
                    else if (_process.HasExited)
                    {
                        if (_process.ExitCode == 1001)
                            await LogHelper.LogInfo("Resetting process for scheduled restart...");
                        else if (_process.ExitCode == 1002)
                        {
                            await LogHelper.LogInfo("Global shutdown requested...");
                            pipe?.Dispose();
                            return;
                        }
                        else
                            await LogHelper.LogInfo("Restarting process due to unexpected shutdown...");

                        _process = null;
                    }

                    await Task.Delay(10);
                    if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape)
                    {
                        await LogHelper.LogInfo("Stopping bot and closing restarter...");

                        /*if (!_process.CloseMainWindow())
                        {
                            _process?.Close();
                        }*/
                        if (pipe.IsConnected)
                        {
                            await LogHelper.LogInfo("Sending SIGTERM...");
                            pipe?.WriteByte(1);
                        }
                        else
                        {
                            _process?.CloseMainWindow();
                        }

                        while (!_process.HasExited)
                        {
                            await Task.Delay(10);
                        }

                        _process?.Close();

                        _active = false;
                        pipe?.Dispose();
                        return;
                    }

                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("", ex);
            }
        }

        private static async Task StartBot()
        {
            try
            {
                var file = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                    $"ThunderED{(IsLinux ? null : ".exe")}");
                await LogHelper.LogInfo($"Starting the bot: {file}");

                var start = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    FileName = file,
                    WindowStyle = RunAsService ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    WorkingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)),
                };

                _process = new Process { StartInfo = start };
                _process.Start();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("", ex);
            }
        }


    }
}
