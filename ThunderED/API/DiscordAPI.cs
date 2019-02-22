using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Matrix.Xmpp.XHtmlIM;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Modules.Sub;
using LogSeverity = ThunderED.Classes.LogSeverity;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class DiscordAPI: CacheBase
    {
        internal DiscordSocketClient Client { get; set; }
        private CommandService Commands { get; }

        public bool IsAvailable { get; private set; }

        public DiscordAPI()
        {
            Client = new DiscordSocketClient();
            Commands = new CommandService();

            Client.Log += async message =>
            {
                await LogHelper.Log(message.Message, message.Severity.ToSeverity(), LogCat.Discord);
                if (message.Exception != null)
                    await LogHelper.LogEx("Discord Internal Exception", message.Exception).ConfigureAwait(false);
            };
            Client.UserJoined += Event_UserJoined;
            Client.Ready += Event_Ready;


        }

        public async Task ReplyMessageAsync(ICommandContext context, string message)
        {
            await ReplyMessageAsync(context, message, false);
        }

        public string GetUserMention(ulong userId)
        {
            return GetGuild()?.GetUser(userId)?.Mention;
        }

        public async Task ReplyMessageAsync(ICommandContext context, string message, bool mentionSender)
        {
            if (context?.Message == null) return;
            if (mentionSender)
            {
                var mention = await GetMentionedUserString(context);
                message = $"{mention}, {message}";
            }

            await ReplyMessageAsync(context, message, null);
        }

        public async Task ReplyMessageAsync(ICommandContext context, IMessageChannel channel, string message, bool mentionSender = false)
        {
            if (context == null || channel == null) return;
            if (mentionSender)
            {
                var mention = await GetMentionedUserString(context);
                message = $"{mention}, {message}";
            }

            try
            {
                await channel.SendMessageAsync(message.FixedLength(MAX_MSG_LENGTH));
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == 50013)
                    await LogHelper.LogError($"The bot don't have rights to send message to {context.Message.Channel.Id} ({context.Message.Channel.Name}) channel!");
                throw;
            }
        }

        public async Task ReplyMessageAsync(ICommandContext context, string message, Embed embed)
        {
            if (context?.Message == null) return;
            try
            {
                await context.Message.Channel.SendMessageAsync(message.FixedLength(MAX_MSG_LENGTH), false, embed).ConfigureAwait(false);
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == 50013)
                    await LogHelper.LogError($"The bot don't have rights to send message to {context.Message.Channel.Id} ({context.Message.Channel.Name}) channel!");
                throw;
            }
        }

        public async Task<IUserMessage> SendMessageAsync(ulong channel, string message, Embed embed = null)
        {
            if (channel == 0) return null;
            var ch = GetChannel(channel);
            if (ch == null)
            {
                await LogHelper.LogWarning($"Discord channel {channel} not found!", LogCat.Discord);
                return null;
            }
            return await SendMessageAsync(ch, message.FixedLength(MAX_MSG_LENGTH), embed);
        }


        public async Task<IUserMessage> SendMessageAsync(IMessageChannel channel, string message, Embed embed = null)
        {
            try
            {
                return await channel.SendMessageAsync(message.FixedLength(MAX_MSG_LENGTH), false, embed);
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == 50013)
                    await LogHelper.LogError($"The bot don't have rights to send message to {channel.Id} ({channel.Name}) channel!");
                throw;
            }
        }

        public const int MAX_MSG_LENGTH = 1999;

        public async Task<string> GetMentionedUserString(ICommandContext context)
        {
            var id = context.Message.MentionedUserIds.FirstOrDefault();
            if (id == 0) return context.Message.Author.Mention;
            return (await context.Guild.GetUserAsync(id))?.Mention;
        }

        public async Task Start()
        {
            try
            {
                await InstallCommands();
                await Client.LoginAsync(TokenType.Bot, SettingsManager.Settings.Config.BotDiscordToken);
                await Client.StartAsync();
            }
            catch (HttpRequestException ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Discord);
                await LogHelper.Log("Probably Discord host is unreachable! Try again later.", LogSeverity.Critical, LogCat.Discord);              
            }
            catch (HttpException ex)
            {
                if (ex.Reason.Contains("401"))
                {
                    await LogHelper.LogError($"Check your Discord bot Token and make sure it is NOT a Client ID: {ex.Reason}", LogCat.Discord);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Discord);
            }
        }

        public void Stop()
        {
            Client.StopAsync().GetAwaiter().GetResult();
        }

        private async Task InstallCommands()
        {
            Client.MessageReceived += HandleCommand;
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        }

        private async Task HandleCommand(SocketMessage messageParam)
        {
            if (!(messageParam is SocketUserMessage message)) return;

            if (SettingsManager.Settings.Config.ModuleIRC)
            {
                var module = TickManager.GetModule<IRCModule>();
                module?.SendMessage(message.Channel.Id, message.Author.Id, message.Author.Username, message.Content);
            }


            if (SettingsManager.Settings.Config.ModuleTelegram)
            {
                var name = APIHelper.DiscordAPI.GetGuild().GetUser(message.Author.Id)?.Nickname ?? message.Author.Username;
                TickManager.GetModule<TelegramModule>()?.SendMessage(message.Channel.Id, message.Author.Id, name, message.Content);
            }

            int argPos = 0;

            if (!(message.HasCharPrefix(SettingsManager.Settings.Config.BotDiscordCommandPrefix[0], ref argPos) || message.HasMentionPrefix
                      (Client.CurrentUser, ref argPos))) return;

            var context = new CommandContext(Client, message);

            await Commands.ExecuteAsync(context, argPos, null);
        }

        private async Task Event_Ready()
        {
            IsAvailable = true;
            var name = SettingsManager.Settings.Config.BotDiscordName.Length > 31
                ? SettingsManager.Settings.Config.BotDiscordName.Substring(0, 31)
                : SettingsManager.Settings.Config.BotDiscordName;
            await GetGuild().CurrentUser.ModifyAsync(x => x.Nickname = name);
            await Client.SetGameAsync(SettingsManager.Settings.Config.BotDiscordGame);
        }

        private static async Task Event_UserJoined(SocketGuildUser arg)
        {
            if (SettingsManager.Settings.Config.WelcomeMessage)
            {
                var channel = SettingsManager.Settings.Config.WelcomeMessageChannelId == 0 ? arg.Guild.DefaultChannel : arg.Guild.GetTextChannel(SettingsManager.Settings.Config.WelcomeMessageChannelId);
                var authurl = WebServerModule.GetAuthPageUrl();
                if (!string.IsNullOrWhiteSpace(authurl))
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("welcomeMessage",arg.Mention,authurl, SettingsManager.Settings.Config.BotDiscordCommandPrefix)).ConfigureAwait(false);
                else
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("welcomeAuth", arg.Mention)).ConfigureAwait(false);
            }
        }

        public async Task<string> IsAdminAccess(ICommandContext context)
        {
            if (context.Guild != null)
            {
                var roles = new List<IRole>(context.Guild.Roles);
                var userRoleIDs = (await context.Guild.GetUserAsync(context.User.Id)).RoleIds;
                var roleMatch = SettingsManager.Settings.Config.DiscordAdminRoles;
                if ((from role in roleMatch select roles.FirstOrDefault(x => x.Name == role) into tmp where tmp != null select userRoleIDs.FirstOrDefault(x => x == tmp.Id))
                    .All(check => check == 0)) return LM.Get("comRequirePriv");
            }
            else
            {
                var guild = (await context.Client.GetGuildsAsync()).FirstOrDefault();
                if (guild == null) return "Error getting guild!";
                var roles = new List<IRole>(guild.Roles);
                var userRoleIDs = (await guild.GetUserAsync(context.User.Id)).RoleIds;
                var roleMatch = SettingsManager.Settings.Config.DiscordAdminRoles;
                if ((from role in roleMatch select roles.FirstOrDefault(x => x.Name == role) into tmp where tmp != null select userRoleIDs.FirstOrDefault(x => x == tmp.Id))
                    .All(check => check == 0)) return LM.Get("comRequirePriv");
            }

            await Task.CompletedTask;
            return null;
        }

        #region Cached queries

        private ulong[] _forbiddenPublicChannels;
        private ulong[] _authAllowedChannels;

        internal ulong[] GetConfigForbiddenPublicChannels()
        {
            return _forbiddenPublicChannels ?? (_forbiddenPublicChannels =
                       SettingsManager.Settings.Config.ComForbiddenChannels.ToArray());
        }

        internal ulong[] GetAuthAllowedChannels()
        {
            return _authAllowedChannels ?? (_authAllowedChannels =
                       SettingsManager.Settings.WebAuthModule.ComAuthChannels.ToArray());
        }

        internal override void PurgeCache()
        {
        }

        internal override void ResetCache(string type = null)
        {
            _forbiddenPublicChannels = null;
            _authAllowedChannels = null;
        }

        internal bool IsUserMention(ICommandContext context)
        {
            return context.Message.MentionedUserIds.Count != 0;
        }

        #endregion

        public class RoleSearchResult
        {
            public string GroupName;
            public List<SocketRole> UpdatedRoles = new List<SocketRole>();
            public List<string> ValidManualAssignmentRoles = new List<string>();
        }



        public async Task<RoleSearchResult> GetRoleGroup(long characterID, ulong discordUserId, bool isManualAuth = false)
        {
            var discordGuild = GetGuild();
            var u = discordGuild.GetUser(discordUserId);
            var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", characterID, true);
            var result = new RoleSearchResult();
            if(u != null)
                result.UpdatedRoles.Add(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));

            #region Get personalized foundList

            var groupsToCheck = new List<WebAuthGroup>();
            var authData = await SQLHelper.GetAuthUserByCharacterId(characterID);

            if (!string.IsNullOrEmpty(authData?.GroupName))
            {
                //check specified group for roles
                var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == authData.GroupName).Value;
                if(group != null)
                    groupsToCheck.Add(group);
            }

            if(!groupsToCheck.Any())
            {
                //check only GENERAL auth groups for roles
                //non-general group auth should have group name supplied
                groupsToCheck.AddRange(SettingsManager.Settings.WebAuthModule.AuthGroups.Values.Where(a=> !a.ESICustomAuthRoles.Any() && a.StandingsAuth == null));
            }
            #endregion
            
            string groupName = null;
            var hasAuth = false;

            // Check for Character Roles
            var authResultCharacter = await WebAuthModule.GetAuthGroupByCharacterId(groupsToCheck, characterID);
            if (authResultCharacter != null) 
            {
                var aRoles = discordGuild.Roles.Where(a => authResultCharacter.RoleEntity.DiscordRoles.Contains(a.Name) && !result.UpdatedRoles.Contains(a)).ToList();
                if (aRoles.Count > 0)
                    result.UpdatedRoles.AddRange(aRoles);
                result.ValidManualAssignmentRoles.AddRange( authResultCharacter.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
                groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == authResultCharacter.Group).Key;
                hasAuth = true;
            }

            if (authResultCharacter == null || (authResultCharacter.Group != null && !authResultCharacter.Group.UseStrictAuthenticationMode))
            {
                // Check for Corporation Roles
                var authResultCorporation = await WebAuthModule.GetAuthGroupByCorpId(groupsToCheck, characterData.corporation_id);
                if (authResultCorporation != null)
                {
                    var aRoles = discordGuild.Roles.Where(a => authResultCorporation.RoleEntity.DiscordRoles.Contains(a.Name) && !result.UpdatedRoles.Contains(a)).ToList();
                    if (aRoles.Count > 0)
                        result.UpdatedRoles.AddRange(aRoles);
                    result.ValidManualAssignmentRoles.AddRange(authResultCorporation.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
                    groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == authResultCorporation.Group).Key;
                    hasAuth = true;
                }

                var group = authResultCharacter?.Group ?? authResultCorporation?.Group;

                if (group == null || !group.UseStrictAuthenticationMode)
                {
                    // Check for Alliance Roles
                    var authResultAlliance = await WebAuthModule.GetAuthGroupByAllyId( groupsToCheck, characterData.alliance_id ?? 0);
                    if (authResultAlliance != null) 
                    {
                        var aRoles = discordGuild.Roles.Where( a => authResultAlliance.RoleEntity.DiscordRoles.Contains(a.Name) && !result.UpdatedRoles.Contains(a) ).ToList();
                        if (aRoles.Count > 0)
                            result.UpdatedRoles.AddRange(aRoles);
                        result.ValidManualAssignmentRoles.AddRange( authResultAlliance.Group.ManualAssignmentRoles.Where(a => !result.ValidManualAssignmentRoles.Contains(a)));
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
                var grList = groupsToCheck.Where(a => a.StandingsAuth != null).ToList();
                if (grList.Count > 0)
                {
                    var ar = await WebAuthModule.GetAuthGroupByCharacterId(groupsToCheck, characterID);
                    if (ar != null)
                    {
                        var aRoles = discordGuild.Roles.Where(a=> ar.RoleEntity.DiscordRoles.Contains(a.Name)).ToList();
                        if (aRoles.Count > 0)
                            result.UpdatedRoles.AddRange(aRoles);
                        result.ValidManualAssignmentRoles.AddRange(ar.Group.ManualAssignmentRoles);
                        groupName = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value == ar.Group).Key;

                    }
                }
            }

            if (!hasauth && (isManualAuth || !string.IsNullOrEmpty(authData?.GroupName)))
            {
                var token = await SQLHelper.GetAuthUserByCharacterId(characterID);
                if (token != null && !string.IsNullOrEmpty(token.GroupName) && SettingsManager.Settings.WebAuthModule.AuthGroups.ContainsKey(token.GroupName))
                {
                    var group = SettingsManager.Settings.WebAuthModule.AuthGroups[token.GroupName];
                    if ((!group.AllowedAlliances.Any() || group.AllowedAlliances.Values.All(a => a.Id.All(b=> b == 0))) &&
                        (!group.AllowedCorporations.Any() || group.AllowedCorporations.Values.All(a => a.Id.All(b=> b == 0))) && (!group.AllowedCharacters.Any() || group.AllowedCharacters.Values.Any(a=> a.Id.All(b=> b == 0))) 
                        && group.StandingsAuth == null)
                        groupName = token.GroupName;
                }

                //ordinary guest
                if (string.IsNullOrEmpty(groupName))
                {
                    var grp = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a =>
                        a.Value.AllowedAlliances.Values.All(b => b.Id.All(c => c == 0)) && a.Value.AllowedCorporations.Values.All(b => b.Id.All(c=> c== 0)));
                    if (grp.Value != null)
                    {
                        groupName = grp.Key;
                        var l = grp.Value.AllowedCorporations.SelectMany(a => a.Value.DiscordRoles);
                        var aRoles = discordGuild.Roles.Where(a => l.Contains(a.Name)).ToList();
                        result.UpdatedRoles.AddRange(aRoles);
                        
                        l = grp.Value.AllowedAlliances.SelectMany(a => a.Value.DiscordRoles);
                        aRoles = discordGuild.Roles.Where(a => l.Contains(a.Name)).ToList();
                        result.UpdatedRoles.AddRange(aRoles);
                    }
                }
            }

            result.UpdatedRoles = result.UpdatedRoles.Distinct().ToList();
            result.GroupName = groupName;
            return result;
        }

        public async Task<string> UpdateUserRoles(ulong discordUserId, List<string> exemptRoles, List<string> authCheckIgnoreRoles, bool isManualAuth)
        {
            var discordGuild = GetGuild();
            var u = discordGuild.GetUser(discordUserId);
            try
            {
                if (u != null && (u.Id == Client.CurrentUser.Id || u.IsBot || u.Roles.Any(r => exemptRoles.Contains(r.Name))))
                    return null;
                if(u == null && (discordUserId == Client.CurrentUser.Id))
                    return null;

               // await LogHelper.LogInfo($"Running Auth Check on {u.Username}", LogCat.AuthCheck, false);

                var authUser = await SQLHelper.GetAuthUserByDiscordId(discordUserId);

                if (authUser != null)
                {
                    //get data
                    var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", authUser.CharacterId, true);
                    //skip bad requests
                    if(characterData == null) return null;

                    var remroles = new List<SocketRole>();
                    var result = await GetRoleGroup(authUser.CharacterId, discordUserId, isManualAuth);
                    var isMovingToDump = string.IsNullOrEmpty(result.GroupName) && authUser.IsAuthed;

                    var changed = false;
                    var isAuthed = result.UpdatedRoles.Count > 1;


                    if (isMovingToDump)
                    {
                        if (SettingsManager.Settings.Config.ModuleHRM && SettingsManager.Settings.HRMModule.UseDumpForMembers)
                        {
                            await LogHelper.LogInfo($"{authUser.Data.CharacterName}({authUser.CharacterId}) is being moved into dumpster...", LogCat.AuthCheck);
                            authUser.SetStateDumpster();
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
                        //if role is not ignored and not in valid roles while char is authed
                        if (!authCheckIgnoreRoles.Contains(invalidRole.Name) && !(isAuthed && result.ValidManualAssignmentRoles.Contains(invalidRole.Name)))
                        {
                            remroles.Add(invalidRole);
                            changed = true;
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
                                else await SendMessageAsync(channel, $"{LM.Get("renewingRoles")} {characterData.name} ({u.Username}){stripped}{added}");
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

        internal async Task UpdateAllUserRoles(List<string> exemptRoles, List<string> authCheckIgnoreRoles)
        {
            var discordGuild = GetGuild();
            var discordUsers = discordGuild.Users;
            var dids = discordUsers.Select(a => a.Id).ToList();

            await dids.ParallelForEachAsync(async id =>
            {
                await UpdateUserRoles(id, exemptRoles, authCheckIgnoreRoles, false); 
            });

            await UpdateDBUserRoles(exemptRoles, authCheckIgnoreRoles, dids);
        }

        private async Task UpdateDBUserRoles(List<string> exemptRoles, List<string> authCheckIgnoreRoles, IEnumerable<ulong> dids)
        {
            var ids = (await SQLHelper.GetAuthUsers(2)).Select(a=> a.DiscordId);

            await ids.Where(a => !dids.Contains(a)).ParallelForEachAsync(async id =>
            {
                await UpdateUserRoles(id, exemptRoles, authCheckIgnoreRoles, false); 
            });
        }

        internal async Task SendEmbedKillMessage(ulong channelId, Color color, KillDataEntry km, string radiusMessage, string msg = "")
        {

            //long shipID, long kmId, string shipName, long value, string sysName, string secstatus, string killTime, string cName, string corpName
            //, string aTicker, bool isNpcKill, string atName, string atCorp, string atTicker, int atCount

           // shipID, killmailID, rShipType.name, (long) value,
           // sysName, systemSecurityStatus, killTime, rVictimCharacter == null ? rShipType.name : rVictimCharacter.name, rVictimCorp.name,
          //  rVictimAlliance == null ? "" : $"[{rVictimAlliance.ticker}]", isNPCKill, rAttackerCharacter.name, rAttackerCorp.name,
          //  rAttackerAlliance == null ? null : $"[{rAttackerAlliance.ticker}]", attackers.Length

            msg = msg ?? "";
            var aTicker = km.rVictimAlliance == null ? "" : $"[{km.rVictimAlliance.ticker}]";
            var atTicker = km.rAttackerAlliance == null ? null : $"[{km.rAttackerAlliance.ticker}]";
            var killString = LM.Get("killFeedString", !string.IsNullOrEmpty(radiusMessage) ? "R " : null, km.rShipType?.name, km.value, km.rVictimCharacter == null ? km.rShipType?.name : km.rVictimCharacter.name,
                km.rVictimCorp.name, string.IsNullOrEmpty(aTicker) ? null : aTicker, km.sysName, km.systemSecurityStatus, km.killTime);
            var killedBy = km.isNPCKill ? null : LM.Get("killFeedBy", km.rAttackerCharacter.name, km.rAttackerCorp.name, string.IsNullOrEmpty(atTicker) ? null : atTicker, km.attackers.Length);
            var builder = new EmbedBuilder()
                .WithColor(color)
                .WithThumbnailUrl($"https://image.eveonline.com/Type/{km.shipID}_32.png")
                .WithAuthor(author =>
                {
                    author.WithName($"{killString} {killedBy}")
                        .WithUrl($"https://zkillboard.com/kill/{km.killmailID}/");
                    if (km.isNPCKill) author.WithIconUrl("http://www.panthernet.org/uf/npc2.jpg");
                });
            if (!string.IsNullOrEmpty(radiusMessage))
                builder.AddField(LM.Get("radiusInfoHeader"), radiusMessage, true);

            var embed = builder.Build();
            var channel = GetGuild()?.GetTextChannel(channelId);
            if (channel != null)
                await SendMessageAsync(channel, msg, embed).ConfigureAwait(false);
        }

        public IMessageChannel GetChannel(ulong guildID, ulong noid)
        {                                                    
            return Client.GetGuild(guildID).GetTextChannel(noid);
        }

        public IMessageChannel GetChannel(ulong noid)
        {                                                    
            return GetGuild().GetTextChannel(noid);
        }

        public void SubscribeRelay(IDiscordRelayModule m)
        {
            if(m == null) return;
            m.RelayMessage += async (message, channel) => { await SendMessageAsync(GetChannel(channel), message); };
        }

        public string GetRoleMention(string role)
        {
            var r = GetGuild().Roles.FirstOrDefault(a => a.Name == role);
            if(r == null || !r.IsMentionable) return null;
            return r.Mention;
        }

        public SocketGuild GetGuild()
        {
            return Client.GetGuild(SettingsManager.Settings.Config.DiscordGuildId);
        }

        public SocketRole GetGuildRole(string roleName)
        {
            return GetGuild().Roles.FirstOrDefault(x => x.Name == roleName);
        }

        public SocketRole GetUserRole(SocketGuildUser user, string roleName)
        {
            return user.Roles.FirstOrDefault(x => x.Name == roleName);
        }

        public SocketGuildUser GetUser(ulong authorId)
        {
            return GetGuild().GetUser(authorId);
        }

        public async Task AssignRolesToUser(SocketGuildUser discordUser, List<SocketRole> rolesToAdd)
        {
            foreach (var r in rolesToAdd)
            {
                if (APIHelper.DiscordAPI.GetUserRole(discordUser, r.Name) == null)
                {
                    try
                    {
                        await discordUser.AddRoleAsync(r);
                    }
                    catch (Exception e)
                    {
                        await LogHelper.LogEx($"Unable to assign role {r.Name} to {discordUser.Nickname}", e, LogCat.Discord);
                    }
                }
            }
        }


        public async Task<bool> IsBotPrivateChannel(IMessageChannel contextChannel)
        {
            return contextChannel.GetType() == typeof(SocketDMChannel) && await contextChannel.GetUserAsync(Client.CurrentUser.Id) != null;

        }

        public List<string> GetUserRoleNames(ulong id)
        {
            return GetUser(id).Roles.Select(a => a.Name).ToList();
        }
    }
}
