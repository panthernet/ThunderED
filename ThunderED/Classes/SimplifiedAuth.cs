using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;

namespace ThunderED.Classes
{
    public static class SimplifiedAuth
    {
        public static async Task UpdateInjectedSettings()
        {
            await LoadData(Path.Combine(SettingsManager.DataDirectory, "_simplifiedAuth.txt"));
        }

        public static async Task SaveData(List<string> list, string path = null)
        {
            path ??= Path.Combine(SettingsManager.DataDirectory, "_simplifiedAuth.txt");
            if (list == null || list.Count == 0)
            {
                await File.WriteAllTextAsync(path, "");
            }
            else
            {
                await File.WriteAllLinesAsync(path, list);
            }
        }

        public static async Task<List<SimplifiedAuthEntity>> GetData(string path = null)
        {
            path ??= Path.Combine(SettingsManager.DataDirectory, "_simplifiedAuth.txt");
            var list = new List<SimplifiedAuthEntity>();
            if (!File.Exists(path)) return list;
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) return list;
            if (lines.All(a => a.StartsWith("//"))) return list;

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
                    await LogHelper.LogEx(nameof(GetData), ex, LogCat.SimplAuth);

                }
            }

            return list;
        }

        internal static async Task LoadData(string path = null)
        {
            path ??= Path.Combine(SettingsManager.DataDirectory, "_simplifiedAuth.txt");

            if (!File.Exists(path)) return;
            await LogHelper.LogInfo("Injecting Simplified Auth information...", LogCat.SimplAuth);

            var lines = await GetData(path);
            //each line is a new entry
            foreach (var entry in lines)
            {
                try
                {
                    if (entry.RolesList == null || !entry.RolesList.Any())
                    {
                        await LogHelper.LogWarning($"No Discord roles specified!", LogCat.SimplAuth);
                        continue;
                    }

                    var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key.Equals(entry.Group, StringComparison.OrdinalIgnoreCase)).Value;
                    if (group == null)
                    {
                        await LogHelper.LogWarning($"Group name not found: {entry.Group}!", LogCat.SimplAuth);
                        continue;
                    }


                    var r = await APIHelper.ESIAPI.SearchMemberEntity("SimplAuth", entry.Name, true);
                    if (r.alliance.Any() || r.character.Any() || r.corporation.Any())
                    {
                        var prefix = r.alliance.Any() ? "a:" : (r.corporation.Any() ? "c:" : null);
                        DeleteIfContainsMemberEntry(group.AllowedMembers, entry.Name);
                        await LogHelper.LogInfo($"Injecting simple entity: {prefix}{entry.Name}...");
                        group.AllowedMembers = group.AllowedMembers.Insert(entry.Name, new AuthRoleEntity
                        {
                            Entities = new List<object> { $"{prefix}{entry.Name}" },
                            DiscordRoles = entry.RolesList.ToList()
                        });
                    }

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(LoadData), ex, LogCat.SimplAuth);
                }
            }
            await LogHelper.LogInfo("Simplified Auth processing complete!", LogCat.SimplAuth);

        }


        private static void DeleteIfContainsMemberEntry(Dictionary<string, AuthRoleEntity> groupAllowedMembers, string entryName)
        {
            if (groupAllowedMembers.ContainsKey(entryName))
                groupAllowedMembers.Remove(entryName);
        }
    }
}
