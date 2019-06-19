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

        internal enum KillMailLinkTypes
        {
            character,
            corporation,
            alliance,
            ship,
            system
        }

        internal static string GetKillMailLink(long id, KillMailLinkTypes killMailLinkTypes)
        {
            return $"https://zkillboard.com/{killMailLinkTypes}/{id}/";
        }

        internal async Task SendEmbedKillMessage(List<ulong> channelIds, Color color, KillDataEntry km, string radiusMessage, string msg = "")
        {
            msg = msg ?? "";

            var victimName = $"{LM.Get("killFeedName", $"[{km.rVictimCharacter.name}]({GetKillMailLink(km.victimCharacterID, KillMailLinkTypes.character)})")}";
            var victimCorp = $"{LM.Get("killFeedCorp", $"[{km.rVictimCorp.name}]({GetKillMailLink(km.victimCorpID, KillMailLinkTypes.corporation)})")}";
            var victimAlliance = km.rVictimAlliance == null ? "" : $"{LM.Get("killFeedAlliance", $"[{km.rVictimAlliance.name}]")}({GetKillMailLink(km.victimAllianceID, KillMailLinkTypes.alliance)})";
            var victimShip = $"{LM.Get("killFeedShip", $"[{km.rVictimShipType.name}]({GetKillMailLink(km.victimShipID, KillMailLinkTypes.ship)})")}";


            string[] victimStringArray = new string[] {victimName, victimCorp, victimAlliance, victimShip}; 

            var attackerName = $"{LM.Get("killFeedName", $"[{km.rAttackerCharacter.name}]({GetKillMailLink(km.finalBlowAttackerCharacterId, KillMailLinkTypes.character)})")}";
            var attackerCorp = $"{LM.Get("killFeedCorp", $"[{km.rAttackerCorp.name}]({GetKillMailLink(km.finalBlowAttackerCorpId, KillMailLinkTypes.corporation)})")}";
            var attackerAlliance = $"{LM.Get("killFeedAlliance", $"[{km.rAttackerAlliance.name}]({GetKillMailLink(km.finalBlowAttackerAllyId, KillMailLinkTypes.alliance)})")}";
            var attackerShip = $"{LM.Get("killFeedShip", $"[{km.rAttackerShipType.name}]({GetKillMailLink(km.attackerShipID, KillMailLinkTypes.ship)})")}";

            string[] attackerStringArray = new string[] { attackerName, attackerCorp, attackerAlliance, attackerShip};


            var killFeedDetails = LM.Get("killFeedDetails", km.killTime, km.value.ToString("#,##0 ISk"));
            var killFeedDetailsSystem = LM.Get("killFeedDetailsSystem", $"[{km.sysName}]({GetKillMailLink(km.systemId, KillMailLinkTypes.system)})");

            string[] detailsStringArray = new string[] { killFeedDetails, killFeedDetailsSystem};


            var builder = new EmbedBuilder()
                .WithColor(color)
                .WithThumbnailUrl($"https://image.eveonline.com/Type/{km.victimShipID}_64.png")
                .WithAuthor(author =>
                {
                    author.WithName(LM.Get("killFeedHeader", km.rVictimShipType.name, km.rSystem.name))
                        .WithUrl($"https://zkillboard.com/kill/{km.killmailID}/");
                    if (km.isNPCKill) author.WithIconUrl("http://www.panthernet.org/uf/npc2.jpg");
                })
                .AddField(LM.Get("Victim"), string.Join("\n", victimStringArray.Where(c => !string.IsNullOrWhiteSpace(c))))
                .AddField(LM.Get("Finalblow"), string.Join("\n", attackerStringArray.Where(c => !string.IsNullOrWhiteSpace(c))))
                .AddField(LM.Get("Details"), string.Join("\n", detailsStringArray.Where(c => !string.IsNullOrWhiteSpace(c))));

            if (!string.IsNullOrEmpty(radiusMessage))
                builder.AddField(LM.Get("radiusInfoHeader"), radiusMessage);

            var embed = builder.Build();
            foreach (var id in channelIds)
            {
                var channel = GetGuild()?.GetTextChannel(id);
                if (channel != null)
                    await SendMessageAsync(channel, msg, embed).ConfigureAwait(false);
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

        public SocketRole GetGuildRole(string roleName, bool caseInsensitive = false)
        {
            return caseInsensitive
                ? GetGuild().Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                : GetGuild().Roles.FirstOrDefault(x => x.Name == roleName);
        }

        
        public List<string> GetGuildRoleNames()
        {
            return GetGuild().Roles.Select(a => a.Name).ToList();
        }

        public SocketRole GetUserRole(SocketGuildUser user, string roleName)
        {
            return user.Roles.FirstOrDefault(x => x.Name == roleName);
        }

        public SocketGuildUser GetUser(ulong authorId)
        {
            return GetGuild().GetUser(authorId);
        }

        public async Task<bool> AssignRoleToUser(ulong userId, SocketRole role)
        {
            var discordUser = GetUser(userId);
            if (discordUser == null) return false;
            try
            {
                await discordUser.AddRoleAsync(role);
                return true;
            }
            catch (Exception e)
            {
                await LogHelper.LogEx($"Unable to assign role {role.Name} to {discordUser.Nickname}", e, LogCat.Discord);
                return false;
            }
        }

        public async Task<bool> AssignRoleToUser(ulong userId, string roleName)
        {
            var discordUser = GetUser(userId);
            if (discordUser == null) return false;
            try
            {
                var role = GetGuildRole(roleName, true);
                if (role == null) return false;
                await discordUser.AddRoleAsync(role);
                return true;
            }
            catch (Exception e)
            {
                await LogHelper.LogEx($"Unable to assign role {roleName} to {discordUser?.Nickname}", e, LogCat.Discord);
                return false;
            }
        }

        
        public async Task<bool> StripUserRole(ulong userId, string roleName)
        {
            var discordUser = GetUser(userId);
            if (discordUser == null) return false;
            try
            {
                var role = GetGuildRole(roleName, true);
                if (role == null) return false;
                await discordUser.RemoveRoleAsync(role);
                return true;
            }
            catch (Exception e)
            {
                await LogHelper.LogEx($"Unable to remove role {roleName} from {discordUser?.Nickname}", e, LogCat.Discord);
                return false;
            }
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

        public List<SocketGuildUser> GetUsers(ulong channelId, bool onlineOnly)
        {
            var users = channelId == 0 ? GetGuild().Users.ToList() : GetGuild().GetChannel(channelId).Users.ToList();
            return onlineOnly ? users.Where(a => a.Status != UserStatus.Offline).ToList() : users;
        }

    }
}
