using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Static;
using ThunderED.Modules.Sub;

namespace ThunderED.Classes
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class DiscordCommands : ModuleBase
    {

        internal const string CMD_TQ= "tq";
        [Command(CMD_TQ, RunMode = RunMode.Async), Summary("Reports TQ status")]
        public async Task TQStatus()
        {
            var tq = await APIHelper.ESIAPI.GetServerStatus("ESIAPI");
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("tqStatusText", tq.players > 20 ? LM.Get("online") : LM.Get("offline"), tq.players), true);
        }

        internal const string CMD_TIMERS= "timers";

        [Command(CMD_TIMERS, RunMode = RunMode.Async), Summary("Report timers to bot private")]
        public async Task TimersCommand()
        {
            if (!SettingsManager.Settings.Config.ModuleTimers)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersModuleDisabled"), true);
                return;
            }
            
            if (!await APIHelper.DiscordAPI.IsBotPrivateChannel(Context.Channel))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("onyBotPrivateCommand"), true);
                return;
            }

            if (await APIHelper.DiscordAPI.IsAdminAccess(Context) == null)
            {
                var timers = await TimersModule.GetUpcomingTimersString();
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"```\n{timers}\n```", true);
                return;
            }


            var allys = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsAlliance && a.Id > 0).Select(a => a.Id);
            var corps = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsCorporation && a.Id > 0).Select(a => a.Id);
            var chars = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsCharacter && a.Id > 0).Select(a => a.Id);

            var skip = !allys.Any() && !corps.Any() && !chars.Any();

            var dataList = (await SQLHelper.GetAuthUser(Context.User.Id))?.FirstOrDefault();
            if (skip || (dataList != null && dataList.Count > 0 && dataList.ContainsKey("characterID")))
            {
                var chId = Convert.ToInt64(dataList["characterID"]);
                var ch = await APIHelper.ESIAPI.GetCharacterData("Discord", chId, true);
                if (skip || ch != null)
                {
                    if (!skip && (!ch.alliance_id.HasValue || !allys.Contains(ch.alliance_id.Value) && !corps.Contains(ch.corporation_id) && !chars.Contains((int)chId)))
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersCmdAccessDenied"), true);
                        return;
                    }
                    var timers = await TimersModule.GetUpcomingTimersString();
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"```\n{timers}\n```", true);
                    return;
                }
            }
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersCmdAccessDenied"), true);

        }

        [Command(CMD_TIMERS, RunMode = RunMode.Async), Summary("Report timers to bot private")]
        public async Task TimersCommand2(int value)
        {
            if (!SettingsManager.Settings.Config.ModuleTimers)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersModuleDisabled"), true);
                return;
            }

            if (!await APIHelper.DiscordAPI.IsBotPrivateChannel(Context.Channel))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("onyBotPrivateCommand"), true);
                return;
            }

            if (await APIHelper.DiscordAPI.IsAdminAccess(Context) == null)
            {
                var timers = await TimersModule.GetUpcomingTimersString();
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"```\n{timers}\n```", true);
                return;
            }

            var allys = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsAlliance).Select(a => a.Id);
            var corps = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsCorporation).Select(a => a.Id);
            var chars = SettingsManager.Settings.TimersModule.AccessList.Values.Where(a => a.IsCharacter).Select(a => a.Id);

            var dataList = (await SQLHelper.GetAuthUser(Context.User.Id))?.FirstOrDefault();
            if (dataList != null && dataList.Count > 0 && dataList.ContainsKey("characterId"))
            {
                var chId = Convert.ToInt64(dataList["characterId"]);
                var ch = await APIHelper.ESIAPI.GetCharacterData("Discord", chId, true);
                if (ch != null)
                {
                    if (!ch.alliance_id.HasValue || !allys.Contains(ch.alliance_id.Value) && !corps.Contains(ch.corporation_id) && !chars.Contains((int)chId))
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersCmdAccessDenied"), true);
                        return;
                    }
                    var timers = await TimersModule.GetUpcomingTimersString(value);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"```\n{timers}\n```", true);
                    return;
                }
            }
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersCmdAccessDenied"), true);
        }

        internal const string CMD_TURL = "turl";
        [Command(CMD_TURL, RunMode = RunMode.Async), Summary("Display timers url")]
        public async Task TimersUrl()
        {
            if(SettingsManager.Settings.Config.ModuleTimers)
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersUrlText", string.IsNullOrEmpty(SettingsManager.Settings.TimersModule.TinyUrl) ? WebServerModule.GetTimersAuthURL() : SettingsManager.Settings.TimersModule.TinyUrl), true);
            else await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("timersModuleDisabled"), true);
        }

        internal const string CMD_FWSTATS = "fwstats";
        [Command(CMD_FWSTATS, RunMode = RunMode.Async), Summary("Reports FW status for a faction")]
        public async Task FWStats(string faction)
        {
            if (!SettingsManager.Settings.Config.ModuleFWStats) return;

            switch (faction.ToLower())
            {
                case "caldari":
                case "c":
                    await FWStatsModule.PostFWSTats(faction[0], Context.Channel);
                    break;
                case "gallente":
                case "g":
                    await FWStatsModule.PostFWSTats(faction[0], Context.Channel);
                    break;
                case "amarr":
                case "a":
                    await FWStatsModule.PostFWSTats(faction[0], Context.Channel);
                    break;
                case "minmatar":
                case "m":
                    await FWStatsModule.PostFWSTats(faction[0], Context.Channel);
                    break;
                default:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("fwstats_error"), true);
                    break;
            }
       }

        [Command("help", RunMode = RunMode.Async), Summary("Reports help text.")]
        public async Task Help()
        {
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpText")}");
        }

        [Command("web", RunMode = RunMode.Async), Summary("Displays web site address")]
        public async Task Web()
        {
            if (SettingsManager.Settings.Config.ModuleWebServer)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, WebServerModule.GetWebSiteUrl());
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
            }
        }

        [Command("authurl", RunMode = RunMode.Async), Summary("Displays web auth URL address")]
        public async Task AuthUrl()
        {
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                if(SettingsManager.Settings.WebAuthModule.AuthGroups.Values.Any(a=> a.PreliminaryAuthMode == false))
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, WebServerModule.GetWebSiteUrl());
                else
                {
                    var grp = !string.IsNullOrEmpty(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup) ? SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a=> a.Key == SettingsManager.Settings.WebAuthModule.DefaultAuthGroup) : SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault();
                    if (grp.Value != null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{grp.Key}: {WebServerModule.GetCustomAuthUrl(grp.Value.ESICustomAuthRoles, grp.Key)}");
                    }
                }
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
            }
        }

        [Command("authurl", RunMode = RunMode.Async), Summary("Displays web auth URL address")]
        public async Task AuthUrl2(string group)
        {
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                var grp = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a=> a.Key == group);
                if (grp.Value != null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{grp.Key}: {WebServerModule.GetCustomAuthUrl(grp.Value.ESICustomAuthRoles, grp.Key)}");
                }
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
            }
        }

        [Command("auth", RunMode = RunMode.Async), Summary("Displays web auth URL address")]
        public async Task Auth3(string command, string data)
        {
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                try
                {
                    string code;
                    long characterId;
                    if (!data.All(char.IsDigit))
                    {
                        code = data;
                        characterId = 0;
                    }
                    else
                    {
                        code = null;
                        characterId = Convert.ToInt64(data);
                    }
                    code = code ?? await SQLHelper.PendingUsersGetCode(characterId);

                    //check if entry exists
                    if (await SQLHelper.UserTokensExists(code) == false)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                        return;
                    }

                    var name = await SQLHelper.UserTokensGetName(code);
                    switch (command)
                    {
                        case "accept":
                        {
                            //check if pending users have valid entry
                            if (await SQLHelper.PendingUsersIsEntryActive(code) == false)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }
                            //check if user confirmed application
                            if (await SQLHelper.UserTokensIsConfirmed(code) == false)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authUserNotConfirmed", name));
                                return;
                            }

                            var userGroupName = await SQLHelper.UserTokensGetGroupName(code);
                            var groupRoles = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == userGroupName).Value?.AuthRoles;
                            //check if group exists
                            if (string.IsNullOrEmpty(userGroupName) || groupRoles == null)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authGroupNameNotFound", userGroupName));
                                return;
                            }

                            //check auth rights
                            if (!APIHelper.DiscordAPI.GetUserRoleNames(Context.Message.Author.Id).Any(a => groupRoles.Contains(a)))
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authNoAccessRights"));
                                return;
                            }

                            //authed for action!
                           // code = code ?? await SQLHelper.PendingUsersGetCode(characterId);
                            var discordUserId = await SQLHelper.PendingUsersGetDiscordId(code);

                            await WebAuthModule.AuthUser(Context, code, discordUserId);
                            await SQLHelper.UserTokensSetAuthState(code, 2);
                            return;
                        }
                        case "decline":
                        {
                            //check if pending users have valid entry
                            if (await SQLHelper.PendingUsersIsEntryActive(code) == false)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }

                            characterId = characterId == 0 ? await SQLHelper.PendingUsersGetCharacterId(code) : characterId;

                            await SQLHelper.SQLiteDataDelete("pendingUsers", "characterID", characterId.ToString());
                            await SQLHelper.SQLiteDataDelete("userTokens", "characterID", characterId.ToString());
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDiscordUserDeclined", name));

                            return;
                        }
                        case "confirm":
                            code = code ?? data;

                            if (await SQLHelper.UserTokensExists(code) == false || await SQLHelper.PendingUsersIsEntryActive(code) == false || await SQLHelper.UserTokensHasDiscordId(code))
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }

                            await SQLHelper.UserTokensSetDiscordId(code, Context.Message.Author.Id);
                            await SQLHelper.PendingUsersSetCode(code, Context.Message.Author.Id);
                            await SQLHelper.UserTokensSetAuthState(code, 1);
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDiscordUserConfirmed", name));

                            return;
                        default:
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("invalidCommandSyntax"));
                            return;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"!auth {command} {data}", ex, LogCat.AuthWeb);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("WebRequestUnexpected"));
                }
            }

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
        }

        [Command("help", RunMode = RunMode.Async), Summary("Reports help text.")]
        public async Task Help([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;
            switch (x)
            {
                case "help":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpHelp")}", true);
                    break;   
                case "web":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpWeb")}", true);
                    break;   
                case "auth":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpAuth")}", true);
                    break;                        
                case "authnotify":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpAuthNotify")}", true);
                    break;                        
                case "evetime":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTime")}", true);
                    break;                        
                case "stat":
                case "stats":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpStat")}", true);
                    break;                        
                case "about":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpAbout")}", true);
                    break;                        
                case "char":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpChar")}", true);
                    break;                        
                case "corp":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpCorp")}", true);
                    break;                        
                case CMD_TQ:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTQ")}", true);
                    break;                        
                case "jita":
                case "amarr":
                case "dodixie":
                case "rens":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpJita")}", true);
                    break;                        
                case "pc":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpPc")}", true);
                    break;            
                case CMD_FWSTATS:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpFwstats", CMD_FWSTATS)}", true);
                    break;  
                case CMD_TURL:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTurl")}", true);
                    break;
                case CMD_TIMERS:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTimers")}", true);
                    break;

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("pc", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Pc([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                var forbiddenChannels = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
                if (x == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                }
                else if (forbiddenChannels.Contains(Context.Channel.Id))
                {
                    await ReplyAsync(LM.Get("commandToPrivate"));

                }else
                {
                    await PriceCheckModule.Check(Context, x, "");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("jita", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !jita Tritanium")]
        public async Task Jita([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModulePriceCheck)
            { 
                if (x == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                }
                else
                {
                    await PriceCheckModule.Check(Context, x, "jita");
                }
            }
        }

        [Command("test", RunMode = RunMode.Async), Summary("Test command")]
       // [CheckForRole]
        public async Task Test([Remainder] string x)
        {
            var result = await APIHelper.DiscordAPI.IsAdminAccess(Context);
            if (!string.IsNullOrEmpty(result))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, result, true);
                return;
            }

            var res = x.Split(' ');
            if (res[0] == "km")
            {
                if(res.Length < 2) return;
                await TestModule.DebugKillmailMessage(Context, res[1], res.Length > 2 && Convert.ToBoolean(res[2]));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("amarr", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Amarr([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                if (x == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                }else
                {
                    await PriceCheckModule.Check(Context, x, "amarr");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("rens", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Rens([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                if (x == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                }else
                {
                    await PriceCheckModule.Check(Context, x, "rens");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("dodixie", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Dodixe([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                if (x == null)
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                else
                    await PriceCheckModule.Check(Context, x, "dodixie");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("rehash", RunMode = RunMode.Async), Summary("Rehash settings file")]
       // [CheckForRole]
        public async Task Reshash()
        {
            try
            {
                var result = await APIHelper.DiscordAPI.IsAdminAccess(Context);
                if (!string.IsNullOrEmpty(result))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, result, true);
                    return;
                }

                SettingsManager.UpdateSettings();
                WebServerModule.ModuleConnectors.Clear();
                ZKillLiveFeedModule.Queryables.Clear();
                TickManager.LoadModules();
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, "REHASH COMPLETED", true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("rehash", ex);

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("reauth", RunMode = RunMode.Async), Summary("Reauth all users")]
        //[CheckForRole]
        public async Task Reauth()
        {
            try
            {
                var result = await APIHelper.DiscordAPI.IsAdminAccess(Context);
                if (!string.IsNullOrEmpty(result))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, result, true);
                    return;
                }
                await TickManager.RunModule(typeof(AuthCheckModule), Context, true);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, "Reauth completed!", true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("reauth", ex);
                await Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("auth", RunMode = RunMode.Async), Summary("Auth User")]
        public async Task Auth()
        {
            var channels = APIHelper.DiscordAPI.GetAuthAllowedChannels();
            if(channels.Length != 0 && !channels.Contains(Context.Channel.Id)) return;

            if (SettingsManager.Settings.Config.ModuleWebServer && SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                try
                {
                    var authString = $"http://{SettingsManager.Settings.WebServerModule.WebExternalIP}:{SettingsManager.Settings.WebServerModule.WebExternalPort}/auth.php";
                    if (!string.IsNullOrEmpty(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup))
                    {
                        var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == SettingsManager.Settings.WebAuthModule.DefaultAuthGroup).Value;
                        if(group != null)
                            authString = WebServerModule.GetCustomAuthUrl(group.ESICustomAuthRoles, SettingsManager.Settings.WebAuthModule.DefaultAuthGroup);
                    }
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context,
                        LM.Get("authInvite", authString), true);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("auth", ex);
                    await Task.FromException(ex);
                }
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDisabled"), true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("authnotify", RunMode = RunMode.Async), Summary("Auth User")]
        public async Task AuthNotify()
        {
            var channels = APIHelper.DiscordAPI.GetAuthAllowedChannels();
            if(channels.Length != 0 && !channels.Contains(Context.Channel.Id)) return;

            if (SettingsManager.Settings.Config.ModuleWebServer && SettingsManager.Settings.Config.ModuleNotificationFeed)
            {
                try
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context,
                        LM.Get("authNotifyInvite", WebServerModule.GetAuthNotifyURL()), true);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("authnotify", ex);
                    await Task.FromException(ex);
                }
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDisabled"), true);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("auth", RunMode = RunMode.Async), Summary("Auth User")]
        public async Task Auth([Remainder] string x)
        {
            if (SettingsManager.Settings.Config.ModuleWebServer  && SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                try
                {
                    if (APIHelper.DiscordAPI.IsUserMention(Context))
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authInvite"), true);
                    }
                    else
                    {
                        await WebAuthModule.AuthUser(Context, x, 0);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("auth", ex);
                    await Task.FromException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("evetime", RunMode = RunMode.Async), Summary("EVE TQ Time")]
        public async Task EveTime()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModuleTime)
            {
                try
                {
                    await EveTimeModule.CheckTime(Context);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("evetime", ex);
                    await Task.FromException(ex);
                }
            }
        }

        [Command("stat", RunMode = RunMode.Async), Summary("Ally status")]
        public async Task Stats([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            try
            {
                if(x == "newday") return; //only auto check allowed for this
                await ContinuousCheckModule.Stats(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
                await Task.FromException(ex);
            }
        }

        [Command("stats", RunMode = RunMode.Async), Summary("Ally status")]
        public async Task Stats2([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            try
            {
                if(x == "newday") return; //only auto check allowed for this
                await ContinuousCheckModule.Stats(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
                await Task.FromException(ex);
            }
        }

        [Command("stat", RunMode = RunMode.Async), Summary("Ally status")]
        public async Task Stats()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            try
            {
                await ContinuousCheckModule.Stats(Context, "m");
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
                await Task.FromException(ex);
            }
        }

        [Command("stats", RunMode = RunMode.Async), Summary("Ally status")]
        public async Task Stats2()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            try
            {
                await ContinuousCheckModule.Stats(Context, "m");
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
                await Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("motd", RunMode = RunMode.Async), Summary("Shows MOTD")]
        public async Task Motd()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModuleMOTD)
            {
                try
                {
                   // await Functions.MOTD(Context);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("motd", ex);
                    await Task.FromException(ex);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("ops", RunMode = RunMode.Async), Summary("Shows current Fleetup Operations")]
        public async Task Ops()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModuleFleetup)
            {
                try
                {
                    await FleetUpModule.Ops(Context, null);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("ops", ex);
                    await Task.FromException(ex);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("ops", RunMode = RunMode.Async), Summary("Shows current Fleetup Operations")]
        public async Task Ops([Remainder] string x)
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            if (SettingsManager.Settings.Config.ModuleFleetup)
            {
                try
                {
                    await FleetUpModule.Ops(Context, null);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("ops", ex);
                    await Task.FromException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("about", RunMode = RunMode.Async), Summary("About Opux")]
        public async Task About()
        {
            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                return;


            try
            {
                var forbiddenChannels = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
                if(forbiddenChannels.Contains(Context.Channel.Id)) return;
                await AboutModule.About(Context);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("about", ex);
                await Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("char", RunMode = RunMode.Async), Summary("Character Details")]
        public async Task Char([Remainder] string x)
        {
            try
            {
                var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
                if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                    return;


                if (SettingsManager.Settings.Config.ModuleCharCorp)
                   await CharSearchModule.SearchCharacter(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("char", ex);
                await Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("corp", RunMode = RunMode.Async), Summary("Corporation Details")]
        public async Task Corp([Remainder] string x)
        {
            try
            {
                var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
                if(forbidden.Any() && forbidden.Contains(Context.Channel.Id))
                    return;


                if (SettingsManager.Settings.Config.ModuleCharCorp)
                  await CorpSearchModule.CorpSearch(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("corp", ex);
                await Task.FromException(ex);
            }
        }
    }
}
