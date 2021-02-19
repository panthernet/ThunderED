using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Modules.OnDemand;

namespace ThunderED.Helpers
{
    public static class LogHelper
    {
        private static string _logPath;
        private static LogSeverity? _logSeverity;
        private static ReaderWriterLock _rwl = new ReaderWriterLock();

        public static async Task LogWarning(string message, LogCat cat = LogCat.Default, bool logConsole = true, bool logFile = true)
        {
            await Log(message, LogSeverity.Warning, cat, logConsole, logFile).ConfigureAwait(false);
        }

        public static async Task LogError(string message, LogCat cat = LogCat.Default, bool logConsole = true)
        {
            await Log(message, LogSeverity.Error, cat, logConsole).ConfigureAwait(false);
        }

        public static async Task LogInfo(string message, LogCat cat = LogCat.Default, bool logConsole = true, bool logFile = true)
        {
            await Log(message, LogSeverity.Info, cat, logConsole, logFile).ConfigureAwait(false);
        }

        public static async Task LogModule(string message, LogCat cat = LogCat.Default, bool logConsole = true, bool logFile = false)
        {
            await Log(message, LogSeverity.Module, cat, logConsole, logFile).ConfigureAwait(false);
        }

        public static async Task Log(string message, LogSeverity severity = LogSeverity.Info, LogCat cat = LogCat.Default, bool logConsole = true, bool logFile = true)
        {
            try
            {
                _logPath = _logPath ?? Path.Combine(SettingsManager.DataDirectory, "logs");
                _logSeverity = _logSeverity ?? SettingsManager.Settings?.Config.LogSeverity.ToSeverity() ?? LogSeverity.Module;
                if ((int) _logSeverity > (int) severity) return;

                var file = Path.Combine(_logPath, (SettingsManager.Settings?.Config.UseSingleFileForLogging ?? false) ? "Default.log" : $"{cat}.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                // var cc = Console.ForegroundColor;

                logFile = logFile && !(SettingsManager.Settings?.Config.DisableLogIntoFiles ?? false);

                if (logConsole)
                {
                    var time = DateTime.Now.ToString("HH:mm:ss");
                    var msg = $"{time} [{severity,8}] [{cat,13}]: {message}";
                    if(severity == LogSeverity.Critical || severity == LogSeverity.Error)
                        await SystemLogFeeder.FeedMessage($"```diff\n-{msg}\n```", severity);
                    else if(severity == LogSeverity.Warning)
                        await SystemLogFeeder.FeedMessage($"```diff\n+{msg}\n```", severity);
                    else 
                        await SystemLogFeeder.FeedMessage(msg, severity);

                    if (!SettingsManager.Settings?.Config.RunAsServiceCompatibility ?? true)
                    {
                        try
                        {
                            switch (severity)
                            {
                                case LogSeverity.Critical:
                                case LogSeverity.Error:
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    break;
                                case LogSeverity.Warning:
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    break;
                                case LogSeverity.Info:
                                    Console.ForegroundColor = ConsoleColor.White;
                                    break;
                                case LogSeverity.Verbose:
                                case LogSeverity.Debug:
                                    Console.ForegroundColor = ConsoleColor.DarkGray;
                                    break;
                                case LogSeverity.Module:
                                    Console.ForegroundColor = ConsoleColor.Gray;
                                    break;
                            }

                            Console.WriteLine(msg);
                        }
                        catch
                        {
                            //ignore
                        }
                    }
                }

                if (logFile)
                {
                    var format = (SettingsManager.Settings?.Config.UseSingleFileForLogging ?? false)
                        ? $"{DateTime.Now,-19} [{severity,8}] [{cat,13}]: {message}{Environment.NewLine}"
                        : $"{DateTime.Now,-19} [{severity,8}]: {message}{Environment.NewLine}";

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

            _rwl.AcquireWriterLock(2000);
            try
            {
                File.AppendAllTextAsync(filename, message).GetAwaiter().GetResult();
            }
            finally
            {
                // Ensure that the lock is released.
                _rwl.ReleaseWriterLock();
            }

            await Task.CompletedTask;
        }

        public static async Task LogEx(string message, Exception exception, LogCat cat = LogCat.Default)
        {
            await LogExInternal(message, exception, cat, null);
        }

        public static async Task LogEx(Exception exception, LogCat cat = LogCat.Default, [CallerMemberName]string method = null)
        {
            await LogExInternal(null, exception, cat, method);
        }


        private static async Task LogExInternal(string message, Exception exception, LogCat cat, string methodName)
        {
            try
            {
                _logPath = _logPath ?? Path.Combine(SettingsManager.DataDirectory, "logs");
                var file = Path.Combine(_logPath, (SettingsManager.Settings?.Config.UseSingleFileForLogging ?? false) ? "Default.log" : $"{cat}.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                // if(!SettingsManager.Settings.Config.DisableLogIntoFiles)
                await WriteToResource(file,  $"{DateTime.Now,-19} [{LogSeverity.Critical,8}]: {message} {Environment.NewLine}{exception}{exception.InnerException}{Environment.NewLine}");

                var msg = $"{DateTime.Now,-19} [{LogSeverity.Critical,8}] [{cat,13}]: {message} {methodName}";
                var logConsole = !SettingsManager.Settings?.Config.RunAsServiceCompatibility ?? true;

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

                await SystemLogFeeder.FeedMessage($"```\n{msg}\n```", LogSeverity.Error);

            }
            catch
            {
                // ignored
            }
        }


        public static async Task LogNotification(string notificationType, string notificationText)
        {
            _logPath = _logPath ?? Path.Combine(SettingsManager.DataDirectory, "logs");
            var file = Path.Combine(_logPath, "notifications_lg.log");

            if (!Directory.Exists(_logPath))
                Directory.CreateDirectory(_logPath);

            await File.AppendAllTextAsync(file, $"{notificationType}{Environment.NewLine}{notificationText}{Environment.NewLine}{Environment.NewLine}").ConfigureAwait(false);
        }

        public static async Task LogDebug(string message, LogCat cat, bool logToConsole = false)
        {
            await Log(message, LogSeverity.Debug, cat, logToConsole).ConfigureAwait(false);
        }

        /// <summary>
        /// Safely write into console
        /// </summary>
        /// <param name="message">Message text</param>
        public static void WriteConsole(string message)
        {
            if (!SettingsManager.Settings.Config.RunAsServiceCompatibility)
                System.Console.WriteLine(message);
        }

        /// <summary>
        /// Safely write into console
        /// </summary>
        /// <param name="message">Message text</param>
        public static void WriteConsole(string message, params object[] prms)
        {
            if (!SettingsManager.Settings.Config.RunAsServiceCompatibility)
                System.Console.WriteLine(message, prms);
        }
    }
}
