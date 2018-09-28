using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules;
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
                    await LogHelper.LogEx("Discord Internal Exception", message.Exception);
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
                await channel.SendMessageAsync($"{message}");
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
                await context.Message.Channel.SendMessageAsync($"{message}", false, embed).ConfigureAwait(false);
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
            return await SendMessageAsync(GetChannel(channel), message, embed);
        }


        public async Task<IUserMessage> SendMessageAsync(IMessageChannel channel, string message, Embed embed = null)
        {
            try
            {
                return await channel.SendMessageAsync(message, false, embed);
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == 50013)
                    await LogHelper.LogError($"The bot don't have rights to send message to {channel.Id} ({channel.Name}) channel!");
                throw;
            }
        }

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
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly());
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

            await Commands.ExecuteAsync(context, argPos);
        }

        private async Task Event_Ready()
        {
            IsAvailable = true;

            await GetGuild().CurrentUser.ModifyAsync(x => x.Nickname = SettingsManager.Settings.Config.BotDiscordName);
            await Client.SetGameAsync(SettingsManager.Settings.Config.BotDiscordGame);
        }

        private static async Task Event_UserJoined(SocketGuildUser arg)
        {
            if (SettingsManager.Settings.Config.WelcomeMessage)
            {
                var channel = SettingsManager.Settings.Config.WelcomeMessageChannelId == 0 ? arg.Guild.DefaultChannel : arg.Guild.GetTextChannel(SettingsManager.Settings.Config.WelcomeMessageChannelId);
                var authurl = $"http://{SettingsManager.Settings.WebServerModule.WebExternalIP}:{SettingsManager.Settings.WebServerModule.WebExternalPort}/auth.php";
                if (!string.IsNullOrWhiteSpace(authurl))
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("welcomeMessage",arg.Mention,authurl));
                else
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("welcomeAuth", arg.Mention));
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

        public async Task UpdateAllUserRoles(Dictionary<int, List<string>> foundList, List<string> exemptRoles, List<string> authCheckIgnoreRoles)
        {
            var discordGuild = GetGuild();
            var discordUsers = discordGuild.Users;

            foreach (var u in discordUsers)
            {
                try
                {
                    if (u.Id == Client.CurrentUser.Id || u.IsBot || u.Roles.Any(r => exemptRoles.Contains(r.Name)))
                        continue;

                    await LogHelper.LogInfo($"Running Auth Check on {u.Username}", LogCat.AuthCheck, false);

                    var responce = await SQLHelper.GetAuthUser(u.Id);

                    if (responce.Count > 0)
                    {
                        var characterID = responce.OrderByDescending(x => x["id"]).FirstOrDefault()["characterID"];

                        var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", characterID, true);
                        //skip bad requests
                        if(characterData == null) continue;
                        var corporationData = await APIHelper.ESIAPI.GetCorporationData("authCheck", characterData.corporation_id, true);

                        var roles = new List<SocketRole>();
                        var rolesOrig = new List<SocketRole>(u.Roles);
                        var remroles = new List<SocketRole>();
                        roles.Add(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));
                        bool isInExempt = false;

                        foreach (var role in exemptRoles)
                        {
                            var exemptRole = GetUserRole(u, role);
                            if (exemptRole != null)
                            {
                                roles.Add(exemptRole);
                                isInExempt = true;
                            }
                        }

                        if (foundList.Count == 0)
                            isInExempt = true;

                        //Check for Corp roles
                        if (foundList.ContainsKey(characterData.corporation_id))
                        {
                            var cinfo = foundList[characterData.corporation_id];
                            var aRoles = discordGuild.Roles.Where(a=> cinfo.Contains(a.Name)).ToList();
                            if (aRoles.Count > 0)
                                roles.AddRange(aRoles);
                        }

                        //Check for Alliance roles
                        if (characterData.alliance_id != null)
                        {
                            if (foundList.ContainsKey(characterData.alliance_id ?? 0))
                            {
                                var ainfo = foundList[characterData.alliance_id ?? 0];
                                var aRoles = discordGuild.Roles.Where(a=> ainfo.Contains(a.Name)).ToList();
                                if (aRoles.Count > 0)
                                    roles.AddRange(aRoles);
                            }
                        }

                        bool changed = false;
                        foreach (var role in rolesOrig)
                        {
                            if (roles.FirstOrDefault(x => x.Id == role.Id) == null)
                            {
                                if (!authCheckIgnoreRoles.Contains(role.Name))
                                {
                                    remroles.Add(role);
                                    changed = true;
                                }
                            }
                        }

                        foreach (var role in roles)
                        {
                            if (rolesOrig.FirstOrDefault(x => x.Id == role.Id) == null)
                                changed = true;
                        }

                        if (changed)
                        {
                            roles.Remove(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));
                            if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                            {
                                var channel = discordGuild.GetTextChannel(SettingsManager.Settings.WebAuthModule.AuthReportChannel);
                                await SendMessageAsync(channel, $"{LM.Get("renewingRoles")} {characterData.name} ({u.Username})");
                            }

                            await LogHelper.LogInfo($"Adjusting roles for {characterData.name} ({u.Username})", LogCat.AuthCheck);
                            await u.AddRolesAsync(roles);
                            if(!isInExempt)
                                await u.RemoveRolesAsync(remroles);
                            //remove notifications token if user has been stripped of roles
                           // if (!isInExempt && !isAddedRole && isRemovedRole)
                            //    await SQLHelper.SQLiteDataDelete("notificationsList", "characterID", characterID);
                        }

                        var eveName = characterData.name;

                        if (SettingsManager.Settings.WebAuthModule.EnforceCorpTickers || SettingsManager.Settings.WebAuthModule.EnforceCharName)
                        {
                            var nickname = $"{(SettingsManager.Settings.WebAuthModule.EnforceCorpTickers  ? $"[{corporationData.ticker}] " : null)}{(SettingsManager.Settings.WebAuthModule.EnforceCharName ? eveName : u.Username)}";
                            if (nickname != u.Nickname && !string.IsNullOrWhiteSpace(u.Nickname) || string.IsNullOrWhiteSpace(u.Nickname) && u.Username != nickname)
                            {
                                await u.ModifyAsync(x => x.Nickname = nickname);
                                await LogHelper.LogInfo($"Changed name of {u.Nickname} to {nickname}", LogCat.AuthCheck);
                            }
                        }
                    }
                    else
                    {
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
                                await APIHelper.DiscordAPI.SendMessageAsync(channel, $"{LM.Get("resettingRoles")} {u.Username}");
                                await LogHelper.LogInfo($"Resetting roles for {u.Username}", LogCat.AuthCheck);
                                await u.RemoveRolesAsync(rroles);
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx($"Error removing roles: {ex.Message}", ex, LogCat.AuthCheck);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"Fatal Error: {ex.Message}", ex, LogCat.AuthCheck);
                }
            }
        }

        public async Task SendEmbedKillMessage(ulong channelId, Color color, int shipID, int kmId, string shipName, long value, string sysName, string secstatus, string killTime, string cName, string corpName
            , string aTicker, bool isNpcKill, string atName, string atCorp, string atTicker, int atCount, string radiusMessage, string msg = "")
        {
            msg = msg ?? "";
            var killString = LM.Get("killFeedString", !string.IsNullOrEmpty(radiusMessage) ? "R " : null, shipName, value, cName,
                corpName, string.IsNullOrEmpty(aTicker) ? null : aTicker, sysName, secstatus, killTime);
            var killedBy = isNpcKill ? null : LM.Get("killFeedBy", atName, atCorp, string.IsNullOrEmpty(atTicker) ? null : atTicker, atCount);
            var builder = new EmbedBuilder()
                .WithColor(color)
                .WithThumbnailUrl($"https://image.eveonline.com/Type/{shipID}_32.png")
                .WithAuthor(author =>
                {
                    author.WithName($"{killString} {killedBy}")
                        .WithUrl($"https://zkillboard.com/kill/{kmId}/");
                    if (isNpcKill) author.WithIconUrl("http://www.panthernet.org/uf/npc2.jpg");
                });
            if (!string.IsNullOrEmpty(radiusMessage))
                builder.AddInlineField(LM.Get("radiusInfoHeader"), radiusMessage);

            var embed = builder.Build();
            var channel = GetGuild()?.GetTextChannel(channelId);
            if (channel != null)
                await SendMessageAsync(channel, msg, embed).ConfigureAwait(false);
        }


        public async Task Dupes(SocketUser user)
        {
            if (user == null)
            {
                var discordUsers = GetGuild().Users;

                foreach (var u in discordUsers)
                {
                    int count = 0;
                    var responce = await SQLHelper.GetAuthUser(u.Id, true);
                    foreach (var r in responce)
                    {
                        if (count != 0)
                            await SQLHelper.RunCommand($"DELETE FROM authUsers WHERE id = {r["id"]}");
                        count++;
                    }
                }
            }
            else
            {
                int count = 0;
                var responce = await SQLHelper.GetAuthUser(user.Id, true);
                foreach (var r in responce)
                {
                    if (count != 0)
                        await SQLHelper.RunCommand($"DELETE FROM authUsers WHERE id = {r["id"]}");
                    count++;
                }
            }
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
