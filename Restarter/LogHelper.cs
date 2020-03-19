using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Restarter
{
    public static class LogHelper
    {
        private static string _logPath;
        private static ReaderWriterLock _rwl = new ReaderWriterLock();

        public static async Task LogWarning(string message, bool logConsole = true, bool logFile = true)
        {
            await Log(message, "WARNING", logConsole, logFile).ConfigureAwait(false);
        }

        public static async Task LogError(string message, bool logConsole = true)
        {
            await Log(message, "ERROR", logConsole).ConfigureAwait(false);
        }

        public static async Task LogInfo(string message, bool logConsole = true, bool logFile = true)
        {
            await Log(message, "INFO", logConsole, logFile).ConfigureAwait(false);
        }


        public static async Task Log(string message, string severity, bool logConsole = true, bool logFile = true)
        {
            try
            {
                _logPath = _logPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                var file = Path.Combine(_logPath, "restarter.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                if (logConsole)
                {
                    var time = DateTime.Now.ToString("HH:mm:ss");
                    var msg = $"{time} [{severity,8}]: {message}";

                    try
                    {
                        Console.WriteLine(msg);
                    }
                    catch
                    {
                        //ignore
                    }
                }

                if (logFile)
                {
                    var format = $"{DateTime.Now,-19} [{severity,8}]: {message}{Environment.NewLine}";
                    await WriteToResource(file, format);
                }

            }
            catch
            {
                // ignored
            }
        }

        private static async Task WriteToResource(string filename, string message)
        {
            _rwl.AcquireWriterLock(1000);
            try
            {
                File.AppendAllText(filename, message);
            }
            finally
            {
                // Ensure that the lock is released.
                _rwl.ReleaseWriterLock();
            }

            await Task.Delay(1);
        }

        public static async Task LogEx(string message, Exception exception)
        {
            try
            {
                _logPath = _logPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                var file = Path.Combine(_logPath, "restarter.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                // if(!SettingsManager.Settings.Config.DisableLogIntoFiles)
                await WriteToResource(file,  $"{DateTime.Now,-19} [{"EXCEPTION",8}]: {message} {Environment.NewLine}{exception}{exception.InnerException}{Environment.NewLine}");

                var msg = $"{DateTime.Now,-19} [{"EXCEPTION",8}]: {message}";
                var logConsole = true;

                if (logConsole)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(msg);
                    }
                    catch
                    {
                        //ignore
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

    }
}
