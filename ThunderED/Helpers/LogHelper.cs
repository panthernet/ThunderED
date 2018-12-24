using System;
using System.IO;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Modules.OnDemand;

namespace ThunderED.Helpers
{
    public static class LogHelper
    {
        private static string _logPath;
        private static LogSeverity? _logSeverity;

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
                _logPath = _logPath ?? Path.Combine(SettingsManager.RootDirectory, "logs");
                _logSeverity = _logSeverity ?? SettingsManager.Settings.Config.LogSeverity.ToSeverity();
                if ((int) _logSeverity > (int) severity) return;

                var file = Path.Combine(_logPath, $"{cat}.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                // var cc = Console.ForegroundColor;

                logConsole = !SettingsManager.Settings.Config.RunAsServiceCompatibility && logConsole;

                if (logConsole)
                {
                    var msg = $"{DateTime.Now,-19} [{severity,8}] [{cat}]: {message}";
                    await SystemLogFeeder.FeedMessage(msg, severity == LogSeverity.Critical || severity == LogSeverity.Error);

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
                if (logFile)
                    await File.AppendAllTextAsync(file, $"{DateTime.Now,-19} [{severity,8}]: {message}{Environment.NewLine}");

            }
            catch
            {
                // ignored
            }
        }

        public static async Task LogEx(string message, Exception exception, LogCat cat = LogCat.Default)
        {
            try
            {
                _logPath = _logPath ?? Path.Combine(SettingsManager.RootDirectory, "logs");
                var file = Path.Combine(_logPath, $"{cat}.log");

                if (!Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);

                await File.AppendAllTextAsync(file, $"{DateTime.Now,-19} [{LogSeverity.Critical,8}]: {message} {Environment.NewLine}{exception}{exception.InnerException}{Environment.NewLine}").ConfigureAwait(false);

                
                var msg = $"{DateTime.Now,-19} [{LogSeverity.Critical,8}] [{cat}]: {message}";
                var logConsole = !SettingsManager.Settings.Config.RunAsServiceCompatibility;

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

                await SystemLogFeeder.FeedMessage(msg, true);

            }
            catch
            {
                // ignored
            }
        }


        public static async Task LogNotification(string notificationType, string notificationText)
        {
            _logPath = _logPath ?? Path.Combine(SettingsManager.RootDirectory, "logs");
            var file = Path.Combine(_logPath, "notifications_lg.log");

            if (!Directory.Exists(_logPath))
                Directory.CreateDirectory(_logPath);

            await File.AppendAllTextAsync(file, $"{notificationType}{Environment.NewLine}{notificationText}{Environment.NewLine}{Environment.NewLine}").ConfigureAwait(false);
        }

        public static async Task LogDebug(string message, LogCat cat, bool logToConsole = false)
        {
            await Log(message, LogSeverity.Debug, cat, logToConsole).ConfigureAwait(false);
        }
    }
}
