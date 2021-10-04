using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ThunderED
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public static partial class SettingsManager
    {
        public static int MaxConcurrentThreads => Settings.Config.ConcurrentThreadsCount > 0 ? Settings.Config.ConcurrentThreadsCount : 1;
        public static bool IsNew { get; set; }
        public static string FileSettingsPath;

        public static string FileShipsData;
        public static string FileShipsDataDefault;

        public static string RootDirectory { get; }
        public static string DataDirectory { get; }
        public static string DatabaseFilePath { get; private set; }

        public static string DefaultUserAgent = "ThunderED v" + Program.VERSION;

        public static ThunderSettings Settings { get; private set; }

        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static readonly object UpdateLock = new object();


        public static async Task<string> Prepare(string settingsPath = null)
        {
            try
            {
                await UpdateSettings(settingsPath);
                FileShipsData = Path.Combine(DataDirectory, "shipdata.json");
                FileShipsDataDefault = Path.Combine(RootDirectory, "shipdata.def.json");
                return null;
            }
            catch (Exception ex)
            {
                return $"ERROR -> {ex.Message}";
            }
        }

       
        static SettingsManager()
        {
            RootDirectory = Path.GetDirectoryName(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath);
            DataDirectory = IsLinux ? Path.Combine(RootDirectory, "Data") : RootDirectory;
            if (!Directory.Exists(DataDirectory))
            {
                throw new Exception($"Data directory '{DataDirectory}' do not exist! Make sure you've mounted static volume as described in FAQ!");
            }

            FileSettingsPath = Path.Combine(DataDirectory, "settings.json");
        }

        public static Task UpdateSettings(string settingsPath = null)
        {
            Settings = ThunderSettings.Load(settingsPath ?? FileSettingsPath);

            if (Settings.Database.DatabaseProvider == "sqlite")
                DatabaseFilePath = Path.Combine(DataDirectory, "edb.db");
            return Task.CompletedTask;
        }

    }
}
