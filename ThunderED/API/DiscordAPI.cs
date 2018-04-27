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
using Microsoft.Extensions.Configuration;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using LogSeverity = ThunderED.Classes.LogSeverity;

namespace ThunderED.API
{
    public class DiscordAPI: CacheBase
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
            };
            Client.UserJoined += Event_UserJoined;
            Client.Ready += Event_Ready;

        }

        public async Task SendMessageAsync(ICommandContext context, string message, Embed embed = null)
        {
            var channel = context?.Channel;
            if (context == null)
            {
                var guildID = SettingsManager.GetULong("config", "discordGuildId");
                var chID = SettingsManager.GetULong("config", "discordGeneralChannel");
                var discordGuild = Client.GetGuild(guildID);
                channel = discordGuild.GetTextChannel(chID);
            }

            await channel.SendMessageAsync(message, false, embed);
        }

        public async Task ReplyMessageAsync(ICommandContext context, string message)
        {
            await ReplyMessageAsync(context, message, false);
        }

        public async Task ReplyMessageAsync(ICommandContext context, string message, bool mentionSender)
        {
            if (context?.Message == null) return;
            if (mentionSender)
            {
                var mention = await GetMentionedUserString(context);
                message = $"{mention}, {message}";
            }

            await context.Message.Channel.SendMessageAsync(message);
        }

        public async Task ReplyMessageAsync(ICommandContext context, string message, Embed embed)
        {
            if (context?.Message == null) return;
            await context.Message.Channel.SendMessageAsync($"{message}", false, embed);
        }

        public async Task SendMessageAsync(IMessageChannel channel, string message, Embed embed = null)
        {
            await channel.SendMessageAsync(message, false, embed);
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
                await Client.LoginAsync(TokenType.Bot, SettingsManager.Root.GetSection("config")["botDiscordToken"]);
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
                    await LogHelper.LogError($"Check your Token: {ex.Reason}", LogCat.Discord);
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

            int argPos = 0;

            if (!(message.HasCharPrefix(SettingsManager.Get("config", "botDiscordCommandPrefix")[0], ref argPos) || message.HasMentionPrefix
                      (Client.CurrentUser, ref argPos))) return;

            var context = new CommandContext(Client, message);

            await Commands.ExecuteAsync(context, argPos);
        }

        private async Task Event_Ready()
        {
            IsAvailable = true;

            await Client.GetGuild(SettingsManager.GetULong("config", "discordGuildId"))
                .CurrentUser.ModifyAsync(x => x.Nickname = SettingsManager.Get("config", "botDiscordName"));
            await Client.SetGameAsync(SettingsManager.Get("config", "botDiscordGame"));
        }

        private static async Task Event_UserJoined(SocketGuildUser arg)
        {
            if (SettingsManager.GetBool("config", "welcomeMessage"))
            {
                var channel = arg.Guild.DefaultChannel;
                var authurl = SettingsManager.Get("auth", "authUrl");
                if (!string.IsNullOrWhiteSpace(authurl))
                    await channel.SendMessageAsync(string.Format(LM.Get("welcomeMessage"),arg.Mention,authurl));
                else
                    await channel.SendMessageAsync(string.Format(LM.Get("welcomeMessage"), arg.Mention));
            }
        }

        #region Cached queries

        private ulong[] _forbiddenPublicChannels;
        private ulong[] _authAllowedChannels;

        internal ulong[] GetConfigForbiddenPublicChannels()
        {
            return _forbiddenPublicChannels ?? (_forbiddenPublicChannels =
                       SettingsManager.GetSubList("config", "comForbiddenChannels").Select(a => Convert.ToUInt64(a.Value)).ToArray());
        }

        internal ulong[] GetAuthAllowedChannels()
        {
            return _authAllowedChannels ?? (_authAllowedChannels =
                       SettingsManager.GetSubList("auth", "comAuthChannels").Select(a => Convert.ToUInt64(a.Value)).ToArray());
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

        public async Task UpdateAllUserRoles(ulong guildID, Dictionary<string, string> alliance, Dictionary<string, string> corps, IConfigurationSection[] exemptRoles)
        {
            var discordGuild = Client.GetGuild(guildID);
            var discordUsers = discordGuild.Users;
            var logchan = SettingsManager.GetULong("auth", "authReportChannel");

            foreach (var u in discordUsers)
            {
                try
                {
                    var eRoleNames = exemptRoles.Select(a => a.Value);
                    if (u.Id == Client.CurrentUser.Id || u.IsBot || u.Roles.Any(r => eRoleNames.Contains(r.Name)))
                        continue;


                    await LogHelper.LogInfo($"Running Auth Check on {u.Username}", LogCat.AuthCheck, false);

                    var responce = await SQLiteHelper.GetAuthUser(u.Id);

                    if (responce.Count > 0)
                    {
                        var characterID = responce.OrderByDescending(x => x["id"]).FirstOrDefault()["characterID"];

                        var characterData = await APIHelper.ESIAPI.GetCharacterData("authCheck", characterID, true);
                        var corporationData = await APIHelper.ESIAPI.GetCorporationData("authCheck", characterData.corporation_id, true);

                        var roles = new List<SocketRole>();
                        var rolesOrig = new List<SocketRole>(u.Roles);
                        var remroles = new List<SocketRole>();
                        roles.Add(u.Roles.FirstOrDefault(x => x.Name == "@everyone"));
                        bool isInExempt = false;
                        foreach (var role in exemptRoles)
                        {
                            var exemptRole = u.Roles.FirstOrDefault(x => x.Name == role.Value);
                            if (exemptRole != null)
                            {
                                roles.Add(exemptRole);
                                isInExempt = true;
                            }
                        }

                        bool isAddedRole = false;
                        //Check for Corp roles
                        if (corps.ContainsKey(characterData.corporation_id.ToString()))
                        {
                            var cinfo = corps.FirstOrDefault(x => x.Key == characterData.corporation_id.ToString());
                            var aRole = discordGuild.Roles.FirstOrDefault(x => x.Name == cinfo.Value);
                            if (aRole != null)
                                isAddedRole = true;
                            roles.Add(aRole);
                        }

                        //Check for Alliance roles
                        if (characterData.alliance_id != null)
                        {
                            if (alliance.ContainsKey(characterData.alliance_id.ToString()))
                            {
                                var ainfo = alliance.FirstOrDefault(x => x.Key == characterData.alliance_id.ToString());
                                var aRole = discordGuild.Roles.FirstOrDefault(x => x.Name == ainfo.Value);
                                if (aRole != null)
                                    isAddedRole = true;
                                roles.Add(aRole);
                            }
                        }

                        bool changed = false;
                        bool isRemovedRole = false;
                        foreach (var role in rolesOrig)
                        {
                            if (roles.FirstOrDefault(x => x.Id == role.Id) == null)
                            {
                                remroles.Add(role);
                                changed = true;
                                isRemovedRole = true;
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
                            if (logchan != 0)
                            {
                                var channel = discordGuild.GetTextChannel(logchan);
                                await channel.SendMessageAsync($"{LM.Get("renewingRoles")} {u.Username}");
                            }

                            await LogHelper.LogInfo($"Adjusting roles for {u.Username}", LogCat.AuthCheck);
                            await u.AddRolesAsync(roles);
                            await u.RemoveRolesAsync(remroles);
                            //remove notifications token if user has been stripped of roles
                            if (!isInExempt && !isAddedRole && isRemovedRole)
                                await SQLiteHelper.SQLiteDataDelete("notifications", "characterID", characterID);
                        }

                        var eveName = characterData.name;
                        var corpTickers = SettingsManager.GetBool("auth", "enforceCorpTickers");
                        var nameEnforce = SettingsManager.GetBool("auth", "enforceCharName");

                        if (corpTickers || nameEnforce)
                        {
                            var nickname = $"{(corpTickers ? $"[{corporationData.ticker}] " : null)}{(nameEnforce ? eveName : u.Username)}";
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
                            var exemptRole = exemptRoles.FirstOrDefault(x => x.Value == rrole.Name);
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
                                if (exemptRoles.FirstOrDefault(x => x.Value == exempt.Name) == null)
                                    rchanged = true;
                            }
                        }

                        if (rchanged)
                        {
                            try
                            {
                                var channel = discordGuild.GetTextChannel(logchan);
                                await channel.SendMessageAsync($"{LM.Get("resettingRoles")} {u.Username}");
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
            var killString = string.Format(LM.Get("killFeedString"), !string.IsNullOrEmpty(radiusMessage) ? "R " : null, shipName, value, cName,
                corpName, string.IsNullOrEmpty(aTicker) ? null : aTicker, sysName, secstatus, killTime);
            var killedBy = isNpcKill ? null : string.Format(LM.Get("killFeedBy"), atName, atCorp, string.IsNullOrEmpty(atTicker) ? null : atTicker, atCount);
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
                builder.AddInlineField("Radius Info", radiusMessage);

            var embed = builder.Build();
            var guildID = SettingsManager.GetULong("config", "discordGuildId");
            var discordGuild = Client.Guilds.FirstOrDefault(x => x.Id == guildID);
            var channel = discordGuild?.GetTextChannel(channelId);
            if (channel != null) await channel.SendMessageAsync(msg, false, embed).ConfigureAwait(false);
        }

        internal async Task AuthGrantRoles(ICommandContext context, string characterID, Dictionary<string, string> corps, Dictionary<string, string> alliance, JsonClasses.CharacterData characterData, JsonClasses.CorporationData corporationData, string remainder)
        {
            var rolesToAdd = new List<SocketRole>();
           // var rolesToTake = new List<SocketRole>();

            var allianceID = characterData.alliance_id.ToString();
            var corpID = characterData.corporation_id.ToString();

            try
            {
                var guildID = SettingsManager.GetULong("config", "discordGuildId");
                var alertChannel = SettingsManager.GetULong("auth", "alertChannel");

                var discordGuild = Client.GetGuild(guildID);
                var discordUser = Client.GetGuild(guildID).GetUser(context.Message.Author.Id);

                //Check for Corp roles
                if (corps.ContainsKey(corpID))
                {
                    var cinfo = corps.FirstOrDefault(x => x.Key == corpID);
                    rolesToAdd.Add(discordGuild.Roles.FirstOrDefault(x => x.Name == cinfo.Value));
                }

                //Check for Alliance roles
                if (alliance.ContainsKey(allianceID))
                {
                    var ainfo = alliance.FirstOrDefault(x => x.Key == allianceID);
                    rolesToAdd.Add(discordGuild.Roles.FirstOrDefault(x => x.Name == ainfo.Value));
                }

                foreach (var r in rolesToAdd)
                {
                    if (discordUser.Roles.FirstOrDefault(x => x.Id == r.Id) == null)
                    {
                        var channel = discordGuild.GetTextChannel(alertChannel);
                        await channel.SendMessageAsync(string.Format(LM.Get("grantRolesMessage"), characterData.name));
                        await discordUser.AddRoleAsync(rolesToAdd.First());
                        //await discordUser.AddRolesAsync(rolesToAdd);
                    }
                }
                await SQLiteHelper.SQLiteDataUpdate("pendingUsers", "active", "0", "authString", remainder);

                await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, string.Format(LM.Get("msgAuthSuccess"), context.Message.Author.Mention, characterData.name));
                var eveName = characterData.name;
                var discordID = discordUser.Id;
                var active = "yes";
                var addedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var query2 =
                    $"INSERT OR REPLACE INTO authUsers(eveName, characterID, discordID, role, active, addedOn) VALUES (\"{eveName}\", \"{characterID}\", \"{discordID}\", \"[]\", \"{active}\", \"{addedOn}\")";
                await SQLiteHelper.RunCommand(query2);

                var corpTickers = SettingsManager.GetBool("auth", "enforceCorpTickers");
                var nameEnforce = SettingsManager.GetBool("auth", "enforceCharName");

                if (corpTickers || nameEnforce)
                {
                    var nickname = "";
                    if (corpTickers)
                    {
                        nickname = $"[{corporationData.ticker}] ";
                    }
                    if (nameEnforce)
                    {
                        nickname += $"{eveName}";
                    }
                    else
                    {
                        nickname += $"{discordUser.Username}";
                    }
                    await discordUser.ModifyAsync(x => x.Nickname = nickname);

                    await Dupes(discordUser);
                }
            }

            catch (Exception ex)
            {
                await LogHelper.LogEx($"Failed adding Roles to User {characterData.name}, Reason: {ex.Message}", ex, LogCat.Discord);
            }
        }

        private async Task Dupes(SocketUser user)
        {
            if (user == null)
            {
                var guildID =SettingsManager.GetULong("config", "discordGuildId");
                var discordUsers = Client.GetGuild(guildID).Users;

                foreach (var u in discordUsers)
                {
                    int count = 0;
                    var responce = await SQLiteHelper.GetAuthUser(u.Id, true);
                    foreach (var r in responce)
                    {
                        if (count != 0)
                            await SQLiteHelper.RunCommand($"DELETE FROM authUsers WHERE id = {r["id"]}");
                        count++;
                    }
                }
            }
            else
            {
                int count = 0;
                var responce = await SQLiteHelper.GetAuthUser(user.Id, true);
                foreach (var r in responce)
                {
                    if (count != 0)
                        await SQLiteHelper.RunCommand($"DELETE FROM authUsers WHERE id = {r["id"]}");
                    count++;
                }
            }
        }

        public IMessageChannel GetChannel(ulong guildID, ulong noid)
        {                                                    
            return Client.GetGuild(guildID).GetTextChannel(noid);
        }
    }
}
