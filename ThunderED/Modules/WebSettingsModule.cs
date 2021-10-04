using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public partial  class WebSettingsModule: AppModuleBase
    {
        public sealed override LogCat Category => LogCat.WebSettings;

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Web Settings Editor module...", Category);

            var data = Settings.WebConfigEditorModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.AllowedEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            foreach (var id in GetAllParsedCharacters())
                await APIHelper.ESIAPI.RemoveAllCharacterDataFromCache(id);

            await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.WebConfigEditorModule.GetEnabledGroups().Values.SelectMany(a => a.AllowedDiscordRoles).Distinct().ToList(), Category);

        }

        private async Task SaveTimersAuthData(List<TiData> convData)
        {
            try
            {
                lock (SettingsManager.UpdateLock)
                {
                    var groups = new Dictionary<string, TimersAccessGroup>();
                    Settings.TimersModule.AccessList.Clear();
                    foreach (var data in convData)
                    {
                        if (data.RolesList.Any())
                            data.UpdateRolesFromList();

                        var group = new TimersAccessGroup();
                        var lst = data.Entities.Split(",");
                        foreach (var item in lst)
                        {
                            if (long.TryParse(item, out var value))
                                group.FilterEntities.Add(value);
                            else group.FilterEntities.Add(item);
                        }

                        group.FilterDiscordRoles = data.Roles.Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim()).ToList();
                        groups.Add(data.Name, group);
                    }

                    foreach (var @group in groups)
                        Settings.TimersModule.AccessList.Add(group.Key, group.Value);

                    Settings.Save();
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(SaveTimersAuthData), ex, Category);
            }
        }

        private async Task<WCEAccessFilter> CheckAccess(long characterId, JsonClasses.CharacterData rChar)
        {
            var authgroups = Settings.WebConfigEditorModule.GetEnabledGroups();

            if (authgroups.Count == 0 || authgroups.Values.All(a => !a.AllowedEntities.Any() && !a.AllowedDiscordRoles.Any()))
            {
                return new WCEAccessFilter();
            }

            //check discord roles auth
            foreach (var filter in authgroups.Values)
            {
                if (filter.AllowedDiscordRoles.Any())
                {
                    var authUser = await DbHelper.GetAuthUser(characterId);
                    if (authUser != null && authUser.DiscordId > 0)
                    {
                        if (APIHelper.DiscordAPI.GetUserRoleNames(authUser.DiscordId ?? 0).Intersect(filter.AllowedDiscordRoles).Any())
                            return filter;
                    }
                }
            }

            //check for Discord admins
            var discordId = (await DbHelper.GetAuthUser(characterId))?.DiscordId ?? 0;
            if (discordId > 0 && APIHelper.IsDiscordAvailable)
            {
                var roles = string.Join(',', APIHelper.DiscordAPI.GetUserRoleNames(discordId));
                if (!string.IsNullOrEmpty(roles))
                {
                    var exemptRoles = Settings.Config.DiscordAdminRoles;
                    if(roles.Replace("&br;", "\"").Split(',').Any(role => exemptRoles.Contains(role)))
                        return new WCEAccessFilter();
                }
            }



            foreach (var accessList in Settings.WebConfigEditorModule.GetEnabledGroups())
            {
                var filterName = accessList.Key;
                var filter = accessList.Value;
                var accessChars = GetParsedCharacters(filterName) ?? new List<long>();
                var accessCorps = GetParsedCorporations(filterName) ?? new List<long>();
                var accessAlliance = GetParsedAlliances(filterName) ?? new List<long>();
                if (!accessCorps.Contains(rChar.corporation_id) && (!rChar.alliance_id.HasValue || !(rChar.alliance_id > 0) || (!accessAlliance.Contains(
                                                                        rChar.alliance_id
                                                                            .Value))))
                {
                    if (!accessChars.Contains(characterId))
                    {
                        continue;
                    }
                }

                return filter;
            }

            return null;
        }

        public static bool HasWebAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleWebConfigEditor) return false;
            var m = TickManager.GetModule<WebSettingsModule>();
            return m?.GetAllParsedCharacters().Contains(id) ?? false;
        }
    }

    public class TiData: IIdentifiable
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Entities { get; set; }
        public string Roles { get; set; }

        public IEnumerable<string> RolesList { get; set; } = new List<string>();

        public void UpdateRolesFromList()
        {
            Roles = string.Join(',', RolesList);
        }
        public void UpdateListFromRoles()
        {
            RolesList = Roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Validate()
        {
            return !string.IsNullOrEmpty(Name) && (!string.IsNullOrEmpty(Entities) || Roles.Any());
        }

        public TiData Clone()
        {
            return new TiData
            {
                Entities = Entities,
                RolesList = RolesList,
                Roles = Roles,
                Id = Id,
                Name = Name
            };
        }

        public void UpdateFrom(TiData value)
        {
            Roles = value.Roles;
            RolesList = value.RolesList;
            Name = value.Name;
            Entities = value.Entities;
        }
    }
}
