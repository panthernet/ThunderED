using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;

namespace ThunderED.Classes
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public static partial class SettingsManager
    {
        
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
        public static string FileTemplateSettingsPage_SimpleAuth_Scripts;
        public static string FileTemplateMailAuthSuccess;
        public static string FileTemplateAccessDenied;
        public static string RootDirectory { get; }
        public static string DataDirectory { get; }
        public static string DatabaseFilePath { get; private set; }

        public static DateTime NextNotificationCheck { get; set; }
        public static string DefaultUserAgent = "ThunderED v" + Program.VERSION;
        public static string FileTemplateHRM_Main;
        public static string FileTemplateHRM_Inspect;
        public static string FileTemplateHRM_MailBody;
        public static string FileTemplateHRM_Table;
        public static string FileTemplateHRM_SearchMailPage;

        public static ThunderSettings Settings { get; private set; }

        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);



        public static string Prepare(string settingsPath = null)
        {
            try
            {
                UpdateSettings(settingsPath).GetAwaiter().GetResult();
                FileTemplateMain = Path.Combine(RootDirectory, "Templates", "main.html");
                FileTemplateSettingsPage = Path.Combine(RootDirectory, "Templates", "settingsMain.html");
                FileTemplateSettingsPage_SimpleAuth = Path.Combine(RootDirectory, "Templates", "settingsMain_SimpleAuth.html");
                FileTemplateSettingsPage_SimpleAuth_Scripts = Path.Combine(RootDirectory, "Templates", "settingsMain_SimpleAuth_Scripts.js");
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

        public static async Task UpdateSettings(string settingsPath = null)
        {
            Settings = ThunderSettings.Load(settingsPath ?? FileSettingsPath);

            if (Settings.Database.DatabaseProvider == "sqlite")
                DatabaseFilePath = Path.Combine(DataDirectory, "edb.db");
        }

        public static async Task UpdateInjectedSettings()
        {
            await LoadSimplifiedAuth(Path.Combine(DataDirectory, "_simplifiedAuth.txt"));
        }

        public static async Task SaveSimplifiedAuthData(List<string> list, string path = null)
        {
            path = path ?? Path.Combine(DataDirectory, "_simplifiedAuth.txt");
            if (list == null || list.Count == 0)
            {
                await File.WriteAllTextAsync(path, "");
            }
            else
            {
                await File.WriteAllLinesAsync(path, list);
            }
        }

        public static async Task<List<SimplifiedAuthEntity>> GetSimplifiedAuthData(string path = null)
        {
            path = path ?? Path.Combine(DataDirectory, "_simplifiedAuth.txt");
            var list = new List<SimplifiedAuthEntity>();
            if(!File.Exists(path)) return list;
            var lines = File.ReadAllLines(path);
            if(lines.Length == 0) return list;
            if(lines.All(a=> a.StartsWith("//"))) return list;

            //each line is a new entry
            var count = 1;
            foreach (var entry in lines)
            {
                try
                {
                    if (entry.StartsWith("//") || string.IsNullOrEmpty(entry)) continue;
                    var data = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (data.Length != 3)
                    {
                        await LogHelper.LogWarning($"Error in line: {entry}!", LogCat.SimplAuth);
                        continue;
                    }

                    var name = data[0];
                    var groupName = data[1];
                    var roles = data[2];
                    var discordRoles = roles?.Split(',', StringSplitOptions.RemoveEmptyEntries)?.ToList();
                    list.Add(new SimplifiedAuthEntity
                    {
                        Id = count++,
                        Name = name,
                        Group = groupName,
                        RolesList = discordRoles,
                        Roles = roles
                    });
                }
                catch (Exception ex)
                {
                    await LogHelper.LogWarning($"Error processing entry: {entry}!", LogCat.SimplAuth);
                    await LogHelper.LogEx(nameof(GetSimplifiedAuthData), ex, LogCat.SimplAuth);

                }
            }

            return list;
        }

        private static async Task LoadSimplifiedAuth(string path)
        {
            if(!File.Exists(path) || !Settings.Config.ModuleAuthWeb) return;
            await LogHelper.LogInfo("Injecting Simplified Auth information...", LogCat.SimplAuth);
           
            var lines = await GetSimplifiedAuthData(path);
            //each line is a new entry
            foreach (var entry in lines)
            {
                try
                {
                    if (entry.RolesList == null || entry.RolesList.Count == 0)
                    {
                        await LogHelper.LogWarning($"No Discord roles specified!", LogCat.SimplAuth);
                        continue;
                    }

                    var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key.Equals(entry.Group, StringComparison.OrdinalIgnoreCase)).Value;
                    if (group == null)
                    {
                        await LogHelper.LogWarning($"Group name not found: {entry.Group}!", LogCat.SimplAuth);
                        continue;
                    }

                    var result = (await APIHelper.ESIAPI.SearchAllianceId("SimplAuth", entry.Name))?.alliance?[0] ?? 0;
                    if (result > 0) //alliance
                    {
                        group.AllowedAlliances.Add(entry.Name, new AuthRoleEntity
                        {
                            Id = new List<long> {result},
                            DiscordRoles = entry.RolesList
                        });
                    }
                    else
                    {
                        result = (await APIHelper.ESIAPI.SearchCorporationId("SimplAuth", entry.Name))?.corporation?[0] ?? 0;
                        if (result > 0) //corp
                        {
                            group.AllowedCorporations.Add(entry.Name, new AuthRoleEntity
                            {
                                Id = new List<long> {result},
                                DiscordRoles = entry.RolesList
                            });
                        }
                        else
                        {
                            result = (await APIHelper.ESIAPI.SearchCharacterId("SimplAuth", entry.Name))?.character?[0] ?? 0;
                            if (result > 0) //char
                            {
                                group.AllowedCharacters.Add(entry.Name, new AuthRoleEntity
                                {
                                    Id = new List<long> {result},
                                    DiscordRoles = entry.RolesList
                                });
                            }
                            else
                            {
                                await LogHelper.LogWarning($"Entity not found: {entry.Name}!", LogCat.SimplAuth);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(LoadSimplifiedAuth), ex, LogCat.SimplAuth);
                }
            }
            await LogHelper.LogInfo("Simplified Auth processing complete!", LogCat.SimplAuth);

        }


    }
}
