using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.OnDemand;

namespace ThunderED.Modules
{
    public partial class WebAuthModule
    {
        public class RoleSearchResult
        {
            public string GroupName;
            public List<SocketRole> UpdatedRoles = new List<SocketRole>();
            public List<string> ValidManualAssignmentRoles = new List<string>();
        }

        internal static async Task UpdateAllUserRoles(List<string> exemptRoles, List<string> authCheckIgnoreRoles)
        {
            var discordGuild = APIHelper.DiscordAPI.GetGuild();
            var discordUsers = discordGuild.Users;
            var dids = discordUsers.Select(a => a.Id).ToList();

            if (SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands && DiscordRolesManagementModule.AvailableRoleNames.Any())
            {
                exemptRoles = exemptRoles.ToList();
                exemptRoles.AddRange(DiscordRolesManagementModule.AvailableRoleNames);
            }

            await dids.ParallelForEachAsync(async id =>
            {
                await UpdateUserRoles(id, exemptRoles, authCheckIgnoreRoles, false); 
            }, 8);

            await UpdateDBUserRoles(exemptRoles, authCheckIgnoreRoles, dids);
        }

        private static async Task UpdateDBUserRoles(List<string> exemptRoles, List<string> authCheckIgnoreRoles, IEnumerable<ulong> dids)
        {
            var ids = (await SQLHelper.GetAuthUsers(2)).Where(a=> !a.MainCharacterId.HasValue).Select(a=> a.DiscordId);
            var x = ids.FirstOrDefault(a => a == 268473315843112960);
            await ids.Where(a => !dids.Contains(a)).ParallelForEachAsync(async id =>
            {
                await UpdateUserRoles(id, exemptRoles, authCheckIgnoreRoles, false); 
            }, 8);
        }

        public static async Task<string> UpdateUserRoles(ulong discordUserId, List<string> exemptRoles, List<string> authCheckIgnoreRoles, bool isManualAuth)
        {
            try
            {
                var discordGuild = APIHelper.DiscordAPI.GetGuild();
                var u = discordGuild.GetUser(discordUserId);

                if (u != null && (u.Id == APIHelper.DiscordAPI.Client.CurrentUser.Id || u.IsBot || u.Roles.Any(r => exemptRoles.Contains(r.Name))))
                    return null;
                if(u == null && (discordUserId == APIHelper.DiscordAPI.Client.CurrentUser.Id))
                    return null;

                // await LogHelper.LogInfo($"Running Auth Check on {u.Username}", LogCat.AuthCheck, false);

                var authUser = await SQLHelper.GetAuthUserByDiscordId(discordUserId);

                if (authUser != null)
                {
                    //get data
                    var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", authUser.CharacterId, true);
                    //skip bad requests
                    if(characterData == null) return null;

                    if (authUser.Data.CorporationId != characterData.corporation_id || authUser.Data.AllianceId != characterData.alliance_id)
                    {
                        await authUser.UpdateData(characterData);
                        await SQLHelper.SaveAuthUser(authUser);
                    }
                    var remroles = new List<SocketRole>();
                    var result = await GetRoleGroup(authUser.CharacterId, discordUserId, isManualAuth, authUser.RefreshToken);
                    var isMovingToDump = string.IsNullOrEmpty(result.GroupName) && authUser.IsAuthed;
                    //skip dumped
                    //if (authUser.IsSpying) return null;
                    if (!isMovingToDump)
                    {
                        var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == result.GroupName);
                        //switch group
                        if (result.GroupName != authUser.GroupName)
                        {
                            await LogHelper.LogInfo($"User {authUser.Data.CharacterName}({authUser.CharacterId}) has been transferred from {authUser.GroupName} to {result.GroupName} group", LogCat.AuthCheck);
                            authUser.GroupName = result.GroupName;
                            await SQLHelper.SaveAuthUser(authUser);
                        }
                        isMovingToDump = group.Value == null || (group.Value.IsEmpty() && authUser.GroupName != group.Key);
                    }

                    var changed = false;
                    var isAuthed = result.UpdatedRoles.Count > 1;


                    if (isMovingToDump && !authUser.IsDumped)
                    {
                        if (SettingsManager.Settings.Config.ModuleHRM && SettingsManager.Settings.HRMModule.UseDumpForMembers)
                        {
                            await LogHelper.LogInfo($"{authUser.Data.CharacterName}({authUser.CharacterId}) is being moved into dumpster...", LogCat.AuthCheck);
                            authUser.SetStateDumpster();
                            authUser.GroupName = null;
                            await authUser.UpdateData();
                            await SQLHelper.SaveAuthUser(authUser);
                        }
                        else
                        {
                            await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                        }
                    }
                    if (u == null) return null;


                    var initialUserRoles = new List<SocketRole>(u.Roles);
                    var invalidRoles = initialUserRoles.Where(a => result.UpdatedRoles.FirstOrDefault(b => b.Id == a.Id) == null);
                    foreach (var invalidRole in invalidRoles)
                    {
                        //if role is not ignored
                        if (!authCheckIgnoreRoles.Contains(invalidRole.Name))
                        {
                            // if role is in valid roles and char is not authed
                            if (isAuthed && !result.ValidManualAssignmentRoles.Contains(invalidRole.Name) || !isAuthed)
                            {
                                remroles.Add(invalidRole);
                                changed = true;
                            }
                        }
                    }

                    //mark changed if we have at least one new role to add
                    changed = changed || result.UpdatedRoles.Any(role => initialUserRoles.FirstOrDefault(x => x.Id == role.Id) == null);
    

                    if (changed)
                    {
                        result.UpdatedRoles.Remove(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));

                        var actuallyDone = false;
                        if (result.UpdatedRoles.Count > 0)
                        {
                            try
                            {
                                await u.AddRolesAsync(result.UpdatedRoles);
                                actuallyDone = true;
                            }
                            catch
                            {
                                await LogHelper.LogWarning($"Failed to add {string.Join(',', result.UpdatedRoles.Select(a=> a.Name))} roles to {characterData.name} ({u.Username})!", LogCat.AuthCheck);
                            }

                        }

                        if (remroles.Count > 0)
                        {
                            try
                            {
                                await u.RemoveRolesAsync(remroles);
                                actuallyDone = true;
                            }
                            catch
                            {
                                await LogHelper.LogWarning($"Failed to remove {string.Join(',', remroles.Select(a=> a.Name))} roles from {characterData.name} ({u.Username})!", LogCat.AuthCheck);
                            }
                        }

                        if (actuallyDone)
                        {
                            var stripped = remroles.Count > 0 ? $" {LM.Get("authStripped")}: {string.Join(',', remroles.Select(a => a.Name))}" : null;
                            var added = result.UpdatedRoles.Count > 0 ? $" {LM.Get("authAddedRoles")}: {string.Join(',', result.UpdatedRoles.Select(a => a.Name))}" : null;
                            if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                            {
                                var channel = discordGuild.GetTextChannel(SettingsManager.Settings.WebAuthModule.AuthReportChannel);
                                if(SettingsManager.Settings.WebAuthModule.AuthReportChannel > 0 && channel == null)
                                    await LogHelper.LogWarning($"Discord channel {SettingsManager.Settings.WebAuthModule.AuthReportChannel} not found!", LogCat.Discord);
                                else await APIHelper.DiscordAPI.SendMessageAsync(channel, $"{LM.Get("renewingRoles")} {characterData.name} ({u.Username}){stripped}{added}");
                            }

                            await LogHelper.LogInfo($"Adjusting roles for {characterData.name} ({u.Username}) {stripped}{added}", LogCat.AuthCheck);
                        }
                    }

                    var eveName = characterData.name;

                    if (SettingsManager.Settings.WebAuthModule.EnforceCorpTickers || SettingsManager.Settings.WebAuthModule.EnforceCharName || SettingsManager.Settings.WebAuthModule.EnforceAllianceTickers)
                    {
                        string alliancePart = null;
                        if (SettingsManager.Settings.WebAuthModule.EnforceAllianceTickers && characterData.alliance_id.HasValue)
                        {
                            var ad = await APIHelper.ESIAPI.GetAllianceData("authCheck", characterData.alliance_id.Value, true);
                            alliancePart = ad != null ? $"[{ad.ticker}] " : null;
                        }
                        string corpPart = null;
                        if (SettingsManager.Settings.WebAuthModule.EnforceCorpTickers)
                        {
                            var ad = await APIHelper.ESIAPI.GetCorporationData("authCheck", characterData.corporation_id, true);
                            corpPart = ad != null ? $"[{ad.ticker}] " : null;
                        }

                        var nickname = $"{alliancePart}{corpPart}{(SettingsManager.Settings.WebAuthModule.EnforceCharName ? eveName : u.Username)}";
                        nickname = nickname.Length > 31
                            ? nickname.Substring(0, 31)
                            : nickname;

                        if (nickname != u.Nickname && !string.IsNullOrWhiteSpace(u.Nickname) || string.IsNullOrWhiteSpace(u.Nickname) && u.Username != nickname)
                        {
                            await LogHelper.LogInfo($"Trying to change name of {u.Nickname} to {nickname}", LogCat.AuthCheck);
                            try
                            {
                                await u.ModifyAsync(x => x.Nickname = nickname);
                            }
                            catch
                            {
                                await LogHelper.LogError($"Name change failed, probably due to insufficient rights", LogCat.AuthCheck);
                            }
                        }
                    }

                    return isAuthed && !string.IsNullOrEmpty(result.GroupName) ? result.GroupName : null;
                }
                else
                {
                    if (u == null) return null;
                    var rroles = new List<SocketRole>();
                    var rolesOrig = new List<SocketRole>(u.Roles);
                    foreach (var rrole in rolesOrig)
                    {
                        var exemptRole = exemptRoles.FirstOrDefault(x => x == rrole.Name);
                        if (exemptRole == null)
                        {
                            rroles.Add(rrole);
                        }
                    }

                    rolesOrig.Remove(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));
                    rroles.Remove(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));

                    bool rchanged = false;

                    if (rroles != rolesOrig)
                    {
                        foreach (var exempt in rroles)
                        {
                            if (exemptRoles.FirstOrDefault(x => x == exempt.Name) == null && !authCheckIgnoreRoles.Contains(exempt.Name))
                                rchanged = true;
                        }
                    }

                    if (rchanged)
                    {
                        try
                        {
                            var channel = discordGuild.GetTextChannel(SettingsManager.Settings.WebAuthModule.AuthReportChannel);
                            if(channel != null)
                                await APIHelper.DiscordAPI.SendMessageAsync(channel, $"{LM.Get("resettingRoles")} {u.Username}");
                            await LogHelper.LogInfo($"Resetting roles for {u.Username}", LogCat.AuthCheck);
                            var trueRroles = rroles.Where(a => !exemptRoles.Contains(a.Name) && !authCheckIgnoreRoles.Contains(a.Name));
                            await u.RemoveRolesAsync(trueRroles);
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx($"Error removing roles: {ex.Message}", ex, LogCat.AuthCheck);
                        }
                    }

                    return null;
                }
                
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Fatal Error: {ex.Message}", ex, LogCat.AuthCheck);
                return null;
            }
        }

        private static async Task UpdateResultRolesWithTitles(SocketGuild discordGuild, AuthRoleEntity roleEntity, RoleSearchResult result, long charID, string token)
        {
            var aRoles = discordGuild.Roles.Where(a => roleEntity.DiscordRoles.Contains(a.Name) && !result.UpdatedRoles.Contains(a));
            //process titles in priority
            if (roleEntity.Titles.Any())
            {
                if (string.IsNullOrEmpty(token))
                {
                    await LogHelper.LogWarning(
                        $"User ID {charID} has no ESI token but is being checked against group with Titles! Titles require esi-characters.read_titles.v1 permissions!");
                    return;
                }

                var uToken = await APIHelper.ESIAPI.RefreshToken(token, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                    SettingsManager.Settings.WebServerModule.CcpAppSecret);
                var userTitles = (await APIHelper.ESIAPI.GetCharacterTitles("AuthCheck", charID, uToken))?.Select(a=> a.name).ToList();
                if (userTitles != null && userTitles.Any())
                {

                    foreach (var roleTitle in roleEntity.Titles.Values)
                    {
                        if (!roleTitle.TitleNames.ContainsAnyFromList(userTitles)) continue;
                        foreach (var roleName in roleTitle.DiscordRoles)
                        {
                            var role = APIHelper.DiscordAPI.GetGuildRole(roleName);
                            if (role != null && !result.UpdatedRoles.Contains(role))
                                result.UpdatedRoles.Add(role);
                        }
                    }
                }
            }
            else
                foreach (var role in aRoles)
                {
                    if (!result.UpdatedRoles.Contains(role))
                        result.UpdatedRoles.Add(role);
                }
        }

        public static async Task<RoleSearchResult> GetRoleGroup(long characterID, ulong discordUserId, bool isManualAuth = false, string refreshToken = null)
        {
            var result = new RoleSearchResult();
            var discordGuild = APIHelper.DiscordAPI.GetGuild();
            var u = discordGuild.GetUser(discordUserId);
            var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", characterID, true);

            try
            {
                if (characterData == null)
                    return result;

                if (u != null)
                    result.UpdatedRoles.Add(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));

                #region Get personalized foundList

                var groupsToCheck = new Dictionary<string, WebAuthGroup>();
                var authData = await SQLHelper.GetAuthUserByCharacterId(characterID);

                if (!string.IsNullOrEmpty(authData?.GroupName))
                {
                    //check specified group for roles
                    var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == authData.GroupName);
                    if (group.Value != null)
                    {
                        //process linked groups first
                        if (group.Value.UpgradeGroupNames.Any())
                        {
                            foreach (var item in group.Value.UpgradeGroupNames)
                            {
                                var group2 = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == item);
                                if (group2.Value != null)
                                    groupsToCheck.Add(group2.Key, group2.Value);
                            }
                        }

                        groupsToCheck.Add(group.Key, group.Value);
                    }
                }

                if (!groupsToCheck.Any())
                {
                    //check only GENERAL auth groups for roles
                    //non-general group auth should have group name supplied
                    foreach (var groupPair in SettingsManager.Settings.WebAuthModule.AuthGroups.Where(a => !a.Value.ESICustomAuthRoles.Any() && a.Value.StandingsAuth == null))
                    {
                        groupsToCheck.Add(groupPair.Key, groupPair.Value);
                    }
                }

                #endregion

                string groupName = null;
                var hasAuth = false;

                // Check for Character Roles
                var authResultCharacter = await GetAuthGroupByCharacterId(groupsToCheck, characterID);
                if (authResultCharacter != null)
                {
                    await UpdateResultRolesWithTitles(discordGuild, authResultCharacter.RoleEntity, result, characterID, refreshToken);
                    result.ValidManualAssignmentRoles.AddRange(authResultCharacter.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
                    groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == authResultCharacter.Group).Key;
                    hasAuth = true;
                    groupsToCheck.Clear();
                    groupsToCheck.Add(groupName, authResultCharacter.Group);
                }

                if (authResultCharacter == null || (authResultCharacter.Group != null && !authResultCharacter.Group.UseStrictAuthenticationMode))
                {
                    // Check for Corporation Roles
                    var authResultCorporation = await GetAuthGroupByCorpId(groupsToCheck, characterData.corporation_id);
                    if (authResultCorporation != null)
                    {
                        await UpdateResultRolesWithTitles(discordGuild, authResultCorporation.RoleEntity, result, characterID, refreshToken);
                        result.ValidManualAssignmentRoles.AddRange(authResultCorporation.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
                        groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == authResultCorporation.Group).Key;
                        hasAuth = true;
                        groupsToCheck.Clear();
                        groupsToCheck.Add(groupName, authResultCorporation.Group);
                    }

                    var group = authResultCharacter?.Group ?? authResultCorporation?.Group;

                    if (group == null || !group.UseStrictAuthenticationMode)
                    {
                        // Check for Alliance Roles
                        var authResultAlliance = await GetAuthGroupByAllyId(groupsToCheck, characterData.alliance_id ?? 0);
                        if (authResultAlliance != null)
                        {
                            await UpdateResultRolesWithTitles(discordGuild, authResultAlliance.RoleEntity, result, characterID, refreshToken);
                            result.ValidManualAssignmentRoles.AddRange(authResultAlliance.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
                            groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == authResultAlliance.Group).Key;
                            hasAuth = true;
                        }
                    }
                }

                if (!hasAuth)
                {
                    result.UpdatedRoles = result.UpdatedRoles.Distinct().ToList();
                    result.ValidManualAssignmentRoles = result.ValidManualAssignmentRoles.Distinct().ToList();
                    //search for personal stands
                    var grList = groupsToCheck.Where(a => a.Value.StandingsAuth != null).ToList();
                    if (grList.Count > 0)
                    {
                        var ar = await GetAuthGroupByCharacterId(groupsToCheck, characterID);
                        if (ar != null)
                        {
                            var aRoles = discordGuild.Roles.Where(a => ar.RoleEntity.DiscordRoles.Contains(a.Name)).ToList();
                            if (aRoles.Count > 0)
                                result.UpdatedRoles.AddRange(aRoles);
                            result.ValidManualAssignmentRoles.AddRange(ar.Group.ManualAssignmentRoles);
                            groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == ar.Group).Key;

                        }
                    }
                }

                if (!hasAuth && (isManualAuth || !string.IsNullOrEmpty(authData?.GroupName)))
                {
                    var token = await SQLHelper.GetAuthUserByCharacterId(characterID);
                    if (token != null && !string.IsNullOrEmpty(token.GroupName) && SettingsManager.Settings.WebAuthModule.AuthGroups.ContainsKey(token.GroupName))
                    {
                        var group = SettingsManager.Settings.WebAuthModule.AuthGroups[token.GroupName];
                        if ((!group.AllowedMembers.Any() || group.AllowedMembers.Values.All(a => a.Entities.All(b => b.ToString().All(char.IsDigit) && (long)b == 0)))
                            && group.StandingsAuth == null)
                        {
                            groupName = token.GroupName;
                            var l = group.AllowedMembers.SelectMany(a => a.Value.DiscordRoles);
                            var aRoles = discordGuild.Roles.Where(a => l.Contains(a.Name)).ToList();
                            result.UpdatedRoles.AddRange(aRoles);
                        }
                    }

                    //ordinary guest
                    if (string.IsNullOrEmpty(groupName))
                    {
                        var grp = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a =>
                            a.Value.AllowedMembers.Values.All(b => b.Entities.All(c => c.ToString().All(char.IsDigit) && (long)c == 0)));
                        if (grp.Value != null)
                        {
                            groupName = grp.Key;
                            var l = grp.Value.AllowedMembers.SelectMany(a => a.Value.DiscordRoles);
                            var aRoles = discordGuild.Roles.Where(a => l.Contains(a.Name)).ToList();
                            result.UpdatedRoles.AddRange(aRoles);
                        }
                    }
                }

                result.UpdatedRoles = result.UpdatedRoles.Distinct().ToList();
                result.GroupName = groupName;
                return result;
            }
            catch(Exception ex)
            {
                await LogHelper.LogError($"EXCEPTION: {ex.Message} CHARACTER: {characterID} [{characterData?.name}][{characterData?.corporation_id}]", LogCat.AuthCheck);
                throw;
            }
        }

    }
}
