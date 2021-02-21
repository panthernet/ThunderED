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
        public static string FileTemplateMain;
        public static string FileTemplateAuthPage;
        public static string FileTemplateAuth;
        public static string FileTemplateAuth2;
        public static string FileTemplateAuth3;
        public static string FileTemplateAuthNotifyFail;
        public static string FileTemplateAuthNotifySuccess;

        public static string FileShipsData;
        public static string FileShipsDataDefault;

        public static string FileTemplateTimersPage;
        public static string FileTemplateSettingsPage;
        public static string FileTemplateSettingsPage_SimpleAuth;
        public static string FileTemplateSettingsPage_Timers;
        public static string FileTemplateSettingsPage_SimpleAuth_Scripts;
        public static string FileTemplateSettingsPage_Timers_Scripts;
        public static string FileTemplateMailAuthSuccess;
        public static string FileTemplateAccessDenied;
        public static string RootDirectory { get; }
        public static string DataDirectory { get; }
        public static string DatabaseFilePath { get; private set; }

        public static string DefaultUserAgent = "ThunderED v" + Program.VERSION;
        public static string FileTemplateHRM_Main;
        public static string FileTemplateHRM_Inspect;
        public static string FileTemplateHRM_MailBody;
        public static string FileTemplateHRM_Table;
        public static string FileTemplateHRM_SearchMailPage;

        public static ThunderSettings Settings { get; private set; }

        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);



        public static async Task<string> Prepare(string settingsPath = null)
        {
            try
            {
                await UpdateSettings(settingsPath);
                FileTemplateMain = Path.Combine(RootDirectory, "Templates", "main.html");
                FileTemplateSettingsPage = Path.Combine(RootDirectory, "Templates", "settingsMain.html");
                FileTemplateSettingsPage_SimpleAuth = Path.Combine(RootDirectory, "Templates", "settingsMain_SimpleAuth.html");
                FileTemplateSettingsPage_Timers = Path.Combine(RootDirectory, "Templates", "settingsMain_Timers.html");
                FileTemplateSettingsPage_SimpleAuth_Scripts = Path.Combine(RootDirectory, "Templates", "settingsMain_SimpleAuth_Scripts.js");
                FileTemplateSettingsPage_Timers_Scripts = Path.Combine(RootDirectory, "Templates", "settingsMain_Timers_Scripts.js");
                FileTemplateAuthPage = Path.Combine(RootDirectory, "Templates", "authPage.html");
                FileTemplateAuth = Path.Combine(RootDirectory, "Templates", "auth.html");
                FileTemplateAuth2 = Path.Combine(RootDirectory, "Templates", "auth2.html");
                FileTemplateAuth3 = Path.Combine(RootDirectory, "Templates", "auth3.html");
                FileTemplateAuthNotifyFail = Path.Combine(RootDirectory, "Templates", "authNotifyFail.html");
                FileTemplateAuthNotifySuccess = Path.Combine(RootDirectory, "Templates", "authNotifySuccess.html");
                FileTemplateTimersPage = Path.Combine(RootDirectory, "Templates", "timersMain.html");
                FileTemplateMailAuthSuccess = Path.Combine(RootDirectory, "Templates", "mailAuthSuccess.html");
                FileTemplateAccessDenied = Path.Combine(RootDirectory, "Templates", "accessDenied.html");
                FileTemplateHRM_Main = Path.Combine(RootDirectory, "Templates", "hrm_main.html");
                FileTemplateHRM_Inspect = Path.Combine(RootDirectory, "Templates", "hrm_inspect.html");
                FileTemplateHRM_MailBody = Path.Combine(RootDirectory, "Templates", "hrm_inspect_mail.html");
                FileTemplateHRM_Table = Path.Combine(RootDirectory, "Templates", "hrm_inspect_table.html");
                FileTemplateHRM_SearchMailPage = Path.Combine(RootDirectory, "Templates", "hrmMailSearch.html");
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
