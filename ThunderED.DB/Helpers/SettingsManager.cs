using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace ThunderED
{
    public static class DbSettingsManager
    {
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static DbThunderSettings Settings { get; private set; }

        static DbSettingsManager()
        {
            RootDirectory = Path.GetDirectoryName(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath);
            DataDirectory = IsLinux ? Path.Combine(RootDirectory, "Data") : RootDirectory;
            if (!Directory.Exists(DataDirectory))
            {
                throw new Exception($"Data directory '{DataDirectory}' do not exist! Make sure you've mounted static volume as described in FAQ!");
            }

            FileSettingsPath = Path.Combine(DataDirectory, "settings.json");
            Load();
        }

        public static string FileSettingsPath { get; set; }

        public static string DataDirectory { get; set; }

        public static string RootDirectory { get; set; }

        public static void Load()
        {
            Settings = JsonConvert.DeserializeObject<DbThunderSettings>(File.ReadAllText(FileSettingsPath));

            if (Settings == null)
                throw new Exception("Settings file not found");
        }
    }

    public class DbThunderSettings
    {
        public DbDatabase Database { get; set; } = new DbDatabase();
    }

    public class DbDatabase
    {
        public string DatabaseProvider { get; set; } = "sqlite";
        public string DatabaseFile { get; set; } = "edb.db";
        public string ServerAddress { get; set; }
        public int ServerPort { get; set; }
        public string DatabaseName { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string CustomConnectionString { get; set; }
        public int SqliteBackupFrequencyInHours { get; set; } = 8;
        public int SqliteBackupMaxFiles { get; set; } = 10;
    }

    
}
