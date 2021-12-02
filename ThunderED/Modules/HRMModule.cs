using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public partial class HRMModule: AppModuleBase
    {
        public sealed override LogCat Category => LogCat.HRM;
        
        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing HRM module...", Category);

            foreach (var pair in Settings.HRMModule.SpyFilters)
            {
                var filter = pair.Value;
                filter.CorporationNames.ForEach(async name =>
                {
                    var corp = await APIHelper.ESIAPI.SearchCorporationId(Reason, name);
                    if (corp?.corporation != null && corp.corporation.Any())
                        filter.CorpIds.Add(name, corp.corporation.First());
                });
                filter.AllianceNames.ForEach(async name =>
                {
                    var alliance = await APIHelper.ESIAPI.SearchAllianceId(Reason, name);
                    if (alliance?.alliance != null && alliance.alliance.Any())
                        filter.AllianceIds.Add(name, alliance.alliance.First());
                });
            }

            await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.HRMModule.GetEnabledGroups().Values.SelectMany(a => a.RolesAccessList).Distinct().ToList(), Category);
        }

        private static DateTime _lastUpdateDate = DateTime.MinValue;
        private static DateTime _lastSpyMailUpdateDate = DateTime.MinValue;
        private static DateTime _lastSpyNameUpdateDate = DateTime.MinValue;

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleHRM) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;

            if (IsRunning) return;
            IsRunning = true;
            try
            {
                if ((DateTime.Now - _lastUpdateDate).TotalMinutes >= 25)
                {
                    _lastUpdateDate = DateTime.Now;
                    if (Settings.HRMModule.DumpInvalidationInHours > 0)
                    {
                        var list = await DbHelper.GetAuthUsers(UserStatusEnum.Dumped);
                        foreach (var user in list)
                        {
                            if (user.DumpDate.HasValue && (DateTime.Now - user.DumpDate.Value).TotalHours >= Settings.HRMModule.DumpInvalidationInHours)
                            {
                                await LogHelper.LogInfo($"Disposing dumped member {user.DataView.CharacterName}({user.CharacterId})...");
                                await DbHelper.DeleteAuthUser(user.CharacterId, true);
                            }
                        }
                    }
                }

                if ((DateTime.Now - _lastSpyMailUpdateDate).TotalMinutes >= 10)
                {
                    _lastSpyMailUpdateDate = DateTime.Now;
                    var spies = await DbHelper.GetAuthUsers(UserStatusEnum.Spying, true);
                    foreach (var entity in spies.ToList())
                    {
                        var r = await CheckMailToken(entity);
                        if (r == false)
                        {
                            await LogHelper.LogWarning(
                                $"Mail token for {entity.DataView.CharacterName} is invalid and will be deleted", Category);
                            await DbHelper.DeleteToken(entity.CharacterId, TokenEnum.Mail);
                            spies.Remove(entity);
                        }else if (r == null)
                            spies.Remove(entity);
                    }
                    await MailModule.FeedSpyMail(spies, Settings.HRMModule.DefaultSpiesMailFeedChannelId);
                }

                if ((DateTime.Now - _lastSpyNameUpdateDate).TotalHours >= 12)
                {
                    _lastSpyNameUpdateDate = DateTime.Now;
                    
                    var spies = await DbHelper.GetAuthUsers(UserStatusEnum.Spying);
                    foreach (var spy in spies)
                    {
                        await spy.UpdateData();
                    }
                    await DbHelper.SaveAuthUsers(spies);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }

        }

        private async Task<bool?> CheckMailToken(ThdAuthUser entity)
        {
            var token = await DbHelper.GetToken(entity.CharacterId, TokenEnum.Mail);
            if (token == null)
                return null;
            var result = await APIHelper.ESIAPI.GetAccessToken(token);
            if (result.Data.IsNotValid && !result.Data.IsNoConnection)
                return false;
            return !string.IsNullOrEmpty(result.Result);
        }

        private async Task<HRMAccessFilter> CheckAccess(long characterId)
        {
            var result = Settings.HRMModule.GetEnabledGroups().Values.Where(a=> a.UsersAccessList.Contains(characterId)).ToList();

            if (result.Count > 0) return MergeHRMFilters(result);
            var discordId = (await DbHelper.GetAuthUser(characterId))?.DiscordId ?? 0;
            if (discordId <= 0) return null;
            var discordRoles = APIHelper.DiscordAPI.GetUserRoleNames(discordId);

            var list = Settings.HRMModule.GetEnabledGroups().Values.Where(a => discordRoles.Intersect(a.RolesAccessList).Any());
            return list.Any() ? MergeHRMFilters(list) : null;
        }

        private HRMAccessFilter MergeHRMFilters(IEnumerable<HRMAccessFilter> result)
        {
            var r = new HRMAccessFilter();
            var list = r.GetType().GetProperties().Where(a => a.CanWrite && a.PropertyType == typeof(bool));
            foreach (var propertyInfo in list)
            {
                var st = result.Any(a => (bool)propertyInfo.GetValue(a));
                propertyInfo.SetValue(r, st);
            }

            r.AuthGroupNamesFilter = result.SelectMany(a => a.AuthGroupNamesFilter).Distinct().ToList();
            r.AuthAllianceIdFilter = result.SelectMany(a => a.AuthAllianceIdFilter).Distinct().ToList();
            r.AuthCorporationIdFilter = result.SelectMany(a => a.AuthCorporationIdFilter).Distinct().ToList();

            return r;
            /*return new HRMAccessFilter
            {
                ApplyGroupFilterToAwaitingUsers = result.Any(a=> a.ApplyGroupFilterToAwaitingUsers),
                CanInspectAltUsers = result.Any(a=> a.CanInspectAltUsers),
                CanInspectAuthedUsers = result.Any(a=> a.CanInspectAuthedUsers),
                CanInspectAwaitingUsers = result.Any(a=> a.CanInspectAwaitingUsers),
                CanInspectDumpedUsers = result.Any(a=> a.CanInspectDumpedUsers),
                CanInspectSpyUsers = result.Any(a=> a.CanInspectSpyUsers),
                CanKickUsers = result.Any(a=> a.CanKickUsers),
                CanMoveToSpies = result.Any(a=> a.CanMoveToSpies),
                CanRestoreDumped = result.Any(a=> a.CanRestoreDumped),

                CanSeeIP = result.Any(a => a.CanSeeIP),
                CanFetchToken = result.Any(a => a.CanFetchToken),

                CanSearchMail = result.Any(a=> a.CanSearchMail),
                IsAltUsersVisible = result.Any(a=> a.IsAltUsersVisible),
                IsAuthedUsersVisible = result.Any(a=> a.IsAuthedUsersVisible),
                IsAwaitingUsersVisible = result.Any(a=> a.IsAwaitingUsersVisible),
                IsDumpedUsersVisible = result.Any(a=> a.IsDumpedUsersVisible),
                IsSpyUsersVisible = result.Any(a=> a.IsSpyUsersVisible),
                AuthGroupNamesFilter = result.SelectMany(a=> a.AuthGroupNamesFilter).Distinct().ToList(),
                AuthAllianceIdFilter = result.SelectMany(a=> a.AuthAllianceIdFilter).Distinct().ToList(),
                AuthCorporationIdFilter = result.SelectMany(a=> a.AuthCorporationIdFilter).Distinct().ToList(),
            };*/
        }

        public static bool HasWebAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleHRM) return false;
            if (SettingsManager.Settings.HRMModule.GetEnabledGroups().Values.SelectMany(a => a.UsersAccessList)
                .Contains(id))
                return true;

            var roles = DiscordHelper.GetDiscordRoles(id).GetAwaiter().GetResult();
            if (roles == null) return false;

            foreach (var group in SettingsManager.Settings.HRMModule.AccessList.Values)
            {
                if (group.RolesAccessList != null && roles.Intersect(group.RolesAccessList)
                    .Any())
                    return true;
            }

            return false;
        }
    }

}
