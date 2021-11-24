using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        [Command("help", RunMode = RunMode.Async), Summary("Reports help text.")]
        public async Task Help()
        {
            // await DecompositionHelper.GetPrices(70d, new List<long> { 45510, 46312 });

            if (IsForbidden()) return;                                          
            
            var sb = new StringBuilder();
            sb.Append(LM.Get("helpTextPrivateCommands"));
            sb.Append($": ** {SettingsManager.Settings.Config.BotDiscordCommandPrefix}about | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}tq ");
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                if (SettingsManager.Settings.Config.ModuleTimers)
                {
                    sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}timers");
                }
            }

            if (SettingsManager.Settings.Config.ModuleTime)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}evetime ");
            }

            if (SettingsManager.Settings.Config.ModuleStats)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}stat ");
            }

            if (SettingsManager.Settings.Config.ModuleCharCorp)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}char | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}corp ");
            }

            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}pc | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}jita | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}amarr | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}dodixie | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}rens ");
            }

            if (SettingsManager.Settings.Config.ModuleFWStats)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}fwstats | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}badstand or {SettingsManager.Settings.Config.BotDiscordCommandPrefix}bs ");
            }

            if (SettingsManager.Settings.Config.ModuleLPStock)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}lp ");
            }

            if (SettingsManager.Settings.Config.ModuleContractNotifications)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}clist ");
            }
            if (SettingsManager.Settings.CommandsConfig.EnableShipsCommand)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}ships ");
            }
            if (SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}{CMD_LISTROLES} | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}{CMD_ADDROLE} | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}{CMD_REMROLE} ");
            }
            if (SettingsManager.Settings.Config.ModuleStorageConsole)
            {
                sb.Append($"| {SettingsManager.Settings.Config.BotDiscordCommandPrefix}storage");
            }


            sb.Append("**\n");
            if (string.IsNullOrEmpty(await APIHelper.DiscordAPI.IsAdminAccess(Context)))
            {
                sb.Append(LM.Get("helpTextAdminCommands"));
                sb.Append($": ** {SettingsManager.Settings.Config.BotDiscordCommandPrefix}rehash | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}reauth | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}rngroup | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}sys | {SettingsManager.Settings.Config.BotDiscordCommandPrefix}test km**\n");
            }
            sb.Append(LM.Get("helpExpanded", SettingsManager.Settings.Config.BotDiscordCommandPrefix));

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, sb.ToString());
        }

        [Command("help", RunMode = RunMode.Async), Summary("Reports help text.")]
        public async Task Help([Remainder] string x)
        {
            if(IsForbidden()) return;

            switch (x)
            {
                case "help":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpHelp")}", true);
                    break;   
                case "web":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpWeb")}", true);
                    break;   
                case "auth":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpAuth", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;
                case "evetime":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTime")}", true);
                    break;                        
                case "stat":
                case "stats":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpStat", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;                        
                case "about":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpAbout")}", true);
                    break;                        
                case "char":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpChar", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;                        
                case "corp":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpCorp", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;                        
                case CMD_TQ:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpTQ")}", true);
                    break;                        
                case "jita":
                case "amarr":
                case "dodixie":
                case "rens":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpJita", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;                        
                case "pc":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpPc", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;            
                case "ops":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpOps", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;            
                case CMD_FWSTATS:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpFwstats", CMD_FWSTATS, SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;  
                case CMD_TIMERS:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpTimers", CMD_TIMERS, SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;

                case "rehash":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpRehash")}", true);
                    break;
                case "reauth":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("helpReauth")}", true);
                    break;
                case "lp":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpLp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "lp"), true);
                    break;
                case "badstand":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("badstandHelp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "badstand"), true);
                    break;
                case "rngroup":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpRnGroup", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "rngroup"), true);
                    break;
                case "clist":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpClist", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "clist"), true);
                    break;
                case "ships":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpShips", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "ships"), true);
                    break;
                case CMD_ADDROLE:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpAddRoleCommand", SettingsManager.Settings.Config.BotDiscordCommandPrefix, CMD_ADDROLE), true);
                    break;
                case CMD_REMROLE:
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpRemoveRoleCommand", SettingsManager.Settings.Config.BotDiscordCommandPrefix, CMD_REMROLE), true);
                    break;
                case "storage":
                case "st":
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context,
                        LM.Get("helpSc", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
                    break;
            }
        }

        #region Groups command

        internal const string CMD_ADDROLE= "addrole";

        [Command(CMD_ADDROLE, RunMode = RunMode.Async), Summary("Add manual Discord role")]
        public async Task AddDiscordRole()
        {
            if(!SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands) return;
            if(IsForbidden()) return;
            if (SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Any() &&
                SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Contains(Context.Channel.Id))
                return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpAddRoleCommand", SettingsManager.Settings.Config.BotDiscordCommandPrefix, CMD_ADDROLE), true);
        }
        [Command(CMD_ADDROLE, RunMode = RunMode.Async), Summary("Add manual Discord role")]
        public async Task AddDiscordRole(string role)
        {
            if(!SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands) return;
            if(IsForbidden()) return;
            if (SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Any() &&
                SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Contains(Context.Channel.Id))
                return;

            await DiscordRolesManagementModule.AddRole(Context, role);
        }

        internal const string CMD_REMROLE= "remrole";
        [Command(CMD_REMROLE, RunMode = RunMode.Async), Summary("Add manual Discord role")]
        public async Task RemoveDiscordRole()
        {
            if(!SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands) return;
            if(IsForbidden()) return;
            if (SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Any() &&
                SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Contains(Context.Channel.Id))
                return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpRemoveRoleCommand", SettingsManager.Settings.Config.BotDiscordCommandPrefix, CMD_REMROLE), true);
        }

        [Command(CMD_REMROLE, RunMode = RunMode.Async), Summary("Remove manual Discord role")]
        public async Task RemoveDiscordRole(string role)
        {
            if(!SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands) return;
            if(IsForbidden()) return;
            if (SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Any() &&
                SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Contains(Context.Channel.Id))
                return;

            await DiscordRolesManagementModule.RemoveRole(Context, role);
        }

        internal const string CMD_LISTROLES= "listroles";
        [Command(CMD_LISTROLES, RunMode = RunMode.Async), Summary("List manual Discord roles")]
        public async Task ListDiscordRoles()
        {
            if(!SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands) return;
            if(IsForbidden()) return;
            if (SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Any() &&
                SettingsManager.Settings.CommandsConfig.RolesCommandDiscordChannels.Contains(Context.Channel.Id))
                return;

            await DiscordRolesManagementModule.ListRoles(Context);
        }

        #endregion


        [Command("remind", RunMode = RunMode.Async)]
        public async Task RemindCommand()
        {
            if (!SettingsManager.Settings.Config.ModuleRemind) return;
            if (IsForbidden()) return;

            if (!await APIHelper.DiscordAPI.IsBotPrivateChannel(Context.Channel))
                return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("remWebUrl", ServerPaths.GetRemindUrl()), false);
        }

        #region StorageConsole

        [Command("storage", RunMode = RunMode.Async)]
        public async Task StorageCommand()
        {
            if (!SettingsManager.Settings.Config.ModuleStorageConsole) return;
            if (IsForbidden()) return;

            if (!await IsAllowedByRoles(SettingsManager.Settings.StorageConsoleModule.ListAccessRoles, Context.User.Id))
                return;

            var r = await StorageConsoleModule.GetListCommandResult("list");
            if (r != null)
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, r, true);
        }

        [Command("st", RunMode = RunMode.Async)]
        public async Task StCommand()
        {
            await StorageCommand();
        }

        [Command("st", RunMode = RunMode.Async)]
        public async Task StCommand([Remainder] string command)
        {
            await StorageCommand(command);
        }

        [Command("storage", RunMode = RunMode.Async)]
        public async Task StorageCommand([Remainder] string command)
        {
            if (!SettingsManager.Settings.Config.ModuleStorageConsole) return;
            if (IsForbidden()) return;

            var showHelp = true;

            var lCommand = command?.ToLower();
            if (!string.IsNullOrEmpty(lCommand) && (lCommand.StartsWith("list") || lCommand.StartsWith("add") ||
                                                    lCommand.StartsWith("sub") || lCommand.StartsWith("set") ||
                                                    lCommand.StartsWith("del")))
            {
                if (lCommand.StartsWith("list"))
                {
                    if (!await IsAllowedByRoles(SettingsManager.Settings.StorageConsoleModule.ListAccessRoles, Context.User.Id))
                        return;
                    var r = await StorageConsoleModule.GetListCommandResult(command);
                    if (r == null)
                    {
                    }
                    else
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, r, true);
                        showHelp = false;
                    }
                }
                else
                {
                    if (!await IsAllowedByRoles(SettingsManager.Settings.StorageConsoleModule.EditAccessRoles, Context.User.Id))
                        return;
                    var r = await StorageConsoleModule.UpdateStorage(command);
                    if (r == null)
                    {
                        showHelp = false;
                    }
                    else
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, r, true);
                        showHelp = false;
                    }
                }
            }

            if (showHelp)
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context,
                    LM.Get("helpSc", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
        }

        #endregion

        internal const string CMD_TQ= "tq";
        [Command(CMD_TQ, RunMode = RunMode.Async), Summary("Reports TQ status")]
        public async Task TQStatus()
        {
            if(IsForbidden()) return;
            var tq = await APIHelper.ESIAPI.GetServerStatus("ESIAPI");
            var isOnline = tq.Result != null && tq.Result.players > 20;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("tqStatusText", isOnline ? LM.Get("online") : LM.Get("offline"), tq?.Result.players ?? 0), true);
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

            var allys = TickManager.GetModule<TimersModule>().GetAllAllianceIds();
            var corps = TickManager.GetModule<TimersModule>().GetAllCorporationIds();
            var chars = TickManager.GetModule<TimersModule>().GetAllCharacterIds();

            var skip = !allys.Any() && !corps.Any() && !chars.Any();

            var authUser = await DbHelper.GetAuthUserByDiscordId(Context.User.Id);
            if (skip || authUser != null)
            {
                var ch = await APIHelper.ESIAPI.GetCharacterData("Discord", authUser.CharacterId, true);
                if (skip || ch != null)
                {
                    if (!skip && (!ch.alliance_id.HasValue || !allys.Contains(ch.alliance_id.Value) && !corps.Contains(ch.corporation_id) && !chars.Contains(authUser.CharacterId)))
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

            if(IsForbidden()) return;

            var allys = TickManager.GetModule<TimersModule>().GetAllAllianceIds();
            var corps = TickManager.GetModule<TimersModule>().GetAllCorporationIds();
            var chars = TickManager.GetModule<TimersModule>().GetAllCharacterIds();

            var authUser = await DbHelper.GetAuthUserByDiscordId(Context.User.Id);
            if (authUser != null)
            {
                var ch = await APIHelper.ESIAPI.GetCharacterData("Discord", authUser.CharacterId, true);
                if (ch != null)
                {
                    if (!ch.alliance_id.HasValue || !allys.Contains(ch.alliance_id.Value) && !corps.Contains(ch.corporation_id) && !chars.Contains(authUser.CharacterId))
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


        [Command(CMD_FWSTATS, RunMode = RunMode.Async), Summary("Reports FW status for a faction")]
        public async Task FWStats()
        {
            if (!SettingsManager.Settings.Config.ModuleFWStats) return;
            if(IsForbidden()) return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpFwstats", CMD_FWSTATS, SettingsManager.Settings.Config.BotDiscordCommandPrefix), true);
        }

        [Command("lp", RunMode = RunMode.Async), Summary("Reports LP prices")]
        public async Task LpCommand()
        {
            if (!SettingsManager.Settings.Config.ModuleLPStock) return;
            if(IsForbidden()) return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpLp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "lp"), true);
        }

        [Command("lp", RunMode = RunMode.Async), Summary("Reports LP prices")]
        public async Task LpCommand([Remainder]string command)
        {
            if (!SettingsManager.Settings.Config.ModuleLPStock) return;
            if(IsForbidden()) return;

            var result = await LPStockModule.SendTopLP(Context, command);
            if (!result)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpLp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "lp"), true);
            }
        }

        internal const string CMD_FWSTATS = "fwstats";
        [Command(CMD_FWSTATS, RunMode = RunMode.Async), Summary("Reports FW status for a faction")]
        public async Task FWStats(string faction)
        {
            if (!SettingsManager.Settings.Config.ModuleFWStats) return;
            if(IsForbidden()) return;

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



        [Command("web", RunMode = RunMode.Async), Summary("Displays web site address")]
        public async Task Web()
        {
            if(IsForbidden()) return;
            if (!string.IsNullOrEmpty(SettingsManager.Settings.WebServerModule.WebExternalIP))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, ServerPaths.GetWebSiteUrl());
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
            }
        }

        [Command("rngroup", RunMode = RunMode.Async), Summary("Rename auth group in DB")]
        public async Task RnGroup()
        {
            if(!await IsExecByAdmin()) return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpRnGroup", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "rngroup"), true);
        }

        [Command("rngroup", RunMode = RunMode.Async), Summary("Rename auth group in DB")]
        public async Task RnGroup([Remainder]string command)
        {
            if(!await IsExecByAdmin()) return;

            try
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpRnGroup", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "rngroup"), true);
                    return;
                }

                var data = command.Split('"');
                var from = data.Length > 0 ? data[1] : null;
                var to = data.Length > 2 ? data[3] : null;

                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("rngroupSyntax"), true);
                    return;
                }

                var group = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == to).Value;
                if (group == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("rngroupNotFound"), true);
                    return;
                }

                if (!await DbHelper.IsAuthUsersGroupNameInDB(from))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("rngroupInitialNotFound"), true);
                    return;
                }

                await DbHelper.RenameAuthGroup(from, to);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("rngroupComplete"), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(RnGroup), ex);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("rngroupSyntax"), true);
            }
        }

        [Command("authurl", RunMode = RunMode.Async), Summary("Displays web auth URL address")]
        public async Task AuthUrl()
        {
            if(IsForbidden()) return;

            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                var grps = SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups();
                if(grps.Values.Any(a=> a.PreliminaryAuthMode == false))
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, ServerPaths.GetWebSiteUrl());
                else
                {
                    var grp =
                        !string.IsNullOrEmpty(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup) &&
                        grps.ContainsKey(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)
                            ? grps.FirstOrDefault(a => a.Key == SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)
                            : grps.FirstOrDefault();
                    if (grp.Value != null)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{grp.Key}: {ServerPaths.GetCustomAuthUrl("-", grp.Value.ESICustomAuthRoles, grp.Key)}");
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
                if(!IsAuthAllowed())
                    return;

                var grp = SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups().FirstOrDefault(a=> a.Key == group);
                if (grp.Value != null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{grp.Key}: {ServerPaths.GetCustomAuthUrl("-", grp.Value.ESICustomAuthRoles, grp.Key)}");
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
                    if (SettingsManager.Settings.WebAuthModule.AutoClearAuthCommandsFromDiscord)
                        await APIHelper.DiscordAPI.RemoveMessage(Context.Message);

                    if (!IsAuthAllowed())
                        return;

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

                    var authUser = code == null ? await DbHelper.GetAuthUser(characterId) : await DbHelper.GetAuthUserByRegCode(code);
                    //check if entry exists
                    if (authUser == null)// || !authUser.HasToken)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                        return;
                    }
                    code ??= authUser.RegCode;
                    characterId = characterId > 0 ? characterId : authUser.CharacterId;



                    switch (command)
                    {
                        case "accept":
                        {
                            //check if pending users have valid entry
                            if (string.IsNullOrEmpty(authUser.RegCode))
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }
                            //check if user confirmed application
                            if (authUser.AuthState < 2)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authUserNotConfirmed", authUser.DataView.CharacterName));
                                return;
                            }

                            var groupRoles = SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups().FirstOrDefault(a => a.Key == authUser.GroupName).Value?.AuthRoles;
                            //check if group exists
                            if (string.IsNullOrEmpty(authUser.GroupName) || groupRoles == null)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authGroupNameNotFound", authUser.GroupName));
                                return;
                            }

                            //check auth rights
                            if (!APIHelper.DiscordAPI.GetUserRoleNames(Context.Message.Author.Id).Any(a => groupRoles.Contains(a)))
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authNoAccessRights"));
                                return;
                            }

                            //authed for action!
                            if (authUser.DiscordId > 0)
                            {
                                await WebAuthModule.AuthUser(Context, code, authUser.DiscordId ?? 0, SettingsManager.Settings.Config.DiscordGuildId);
                            }
                            else
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, "Discord ID is missing! User must auth take initial auth before accept op!");
                            }

                            return;
                        }
                        case "decline":
                        {
                            //check if pending users have valid entry
                            if (string.IsNullOrEmpty(authUser.RegCode))
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }

                            await DbHelper.DeleteAuthUser(characterId);
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDiscordUserDeclined", authUser.DataView.CharacterName));

                            return;
                        }
                        case "confirm":

                            if (string.IsNullOrEmpty(authUser.RegCode) || authUser.DiscordId > 0)
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("entryNotFound"));
                                return;
                            }

                            authUser.DiscordId = Context.Message.Author.Id;
                            authUser.SetStateAwaiting();
                            await DbHelper.SaveAuthUser(authUser);
                            await TickManager.GetModule<WebAuthModule>().ProcessPreliminaryApplicant(authUser, Context);
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authDiscordUserConfirmed", authUser.DataView.CharacterName));
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
                    return;
                }
            }
            else
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, $"{LM.Get("webServerOffline")}");
            }

        }

        [Command("clist", RunMode = RunMode.Async), Summary("")]
        public async Task ClistDummy()
        {
            if(IsForbidden())
            {
                await ReplyAsync(LM.Get("commandToPrivate"));
                return;
            }
            if (!SettingsManager.Settings.Config.ModuleContractNotifications) return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context,LM.Get("helpClist", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "clist"), true);
        }

        [Command("clist", RunMode = RunMode.Async), Summary("")]
        public async Task Clist([Remainder] string x)
        {
            if(IsForbidden())
            {
                await ReplyAsync(LM.Get("commandToPrivate"));
                return;
            }

            if (SettingsManager.Settings.Config.ModuleContractNotifications)
            {
                var mod = x?.Split(' ').FirstOrDefault();
                if (string.IsNullOrEmpty(x))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpClist", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "clist"), true);
                    return;
                }
                var groupName = x.Substring(mod.Length, x.Length - mod.Length).Trim();
                var group = SettingsManager.Settings.ContractNotificationsModule.GetEnabledGroups().FirstOrDefault(a => a.Key == groupName);
                if (group.Value == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("clistGroupNotFound", groupName), true);
                    return;
                }

                await ContractNotificationsModule.ProcessClistCommand(Context, group, mod);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("pc", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Pc([Remainder] string x)
        {
            if(IsForbidden()) return;

            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                if (x == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                } 
                else
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
            if(IsForbidden()) return;

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
        public async Task Test([Remainder] string x)
        {
            if(!await IsExecByAdmin()) return;

            var res = x.Split(' ');
            if (res[0] == "km")
            {
                if(res.Length < 2) return;
                await TestModule.DebugKillmailMessage(Context, res[1], res.Length > 2 && Convert.ToBoolean(res[2]));
            }
        }

        [Command("test", RunMode = RunMode.Async), Summary("Test command")]
        public async Task Test()
        {
            if(!await IsExecByAdmin()) return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpTest"), true);

        }

        [Command("sys", RunMode = RunMode.Async), Summary("Test command")]
        public async Task SysCommand()
        {
            if(!await IsExecByAdmin()) return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSys"), true);

        }

        [Command("sys", RunMode = RunMode.Async), Summary("Test command")]
        public async Task SysCommand([Remainder] string x)
        {
            if(!await IsExecByAdmin()) return;

            try
            {
                var values = x.Split(' ');

                switch (values[0])
                {
                    case "help":
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSysText"), true);
                        return;
                    case "cleartable":
                        if (values.Length < 2)
                        {
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSysClear"), true);
                            return;
                        }

                        if(!await SQLHelper.ClearTable(values[1]))
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysClearTableNotFound"), true);
                        else await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysOperationComplete"), true);
                        return;
                    case "esitoken":
                        if (values.Length < 2)
                        {
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSysToken"), true);
                            return;
                        }

                        var user = await DbHelper.GetAuthUser(long.Parse(values[1]), true);
                        if (user == null || !user.HasToken)
                            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysTokenNotFound"), true);
                        else
                        {
                            var t = await APIHelper.ESIAPI.RefreshToken(user.GetGeneralToken(),
                                SettingsManager.Settings.WebServerModule.CcpAppClientId,
                                SettingsManager.Settings.WebServerModule.CcpAppSecret);
                            if(t.Data.IsFailed)
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysTokenNotFound", values[1]), true);
                            else
                            {
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysTokenResult", t.Result),
                                    true);
                            }
                        }
                        return;
                    case "shutdown":
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysShutdownStarted"), true);
                        await Program.Shutdown();
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysShutdownComplete"), true);
                        return;
                    case "restart":
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysRestartStarted"), true);
                        await Program.Restart(Context.Channel.Id);
                        return;
                    case "flush":
                    {
                        var snd = values[1];
                        switch (snd)
                        {
                            case "notifcache":
                                await SQLHelper.RunCommand("delete from notificationsList");
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysFlushedNotifications"), true);
                                return;
                            case "dbcache":
                                await SQLHelper.RunCommand("delete from cache");
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("sysFlushedAllCache"), true);
                                return;
                        }
                        return;
                    }
                    default:
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSys"), true);
                        return;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("reauth", ex);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpSys"), true);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("amarr", RunMode = RunMode.Async), Summary("Performs Prices Checks Example: !pc Tritanium")]
        public async Task Amarr([Remainder] string x)
        {
            if(IsForbidden()) return;

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
            if(IsForbidden()) return;

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
            if(IsForbidden()) return;

            if (SettingsManager.Settings.Config.ModulePriceCheck)
            {
                if (x == null)
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("enterItemName"), true);
                else
                    await PriceCheckModule.Check(Context, x, "dodixie");
            }
        }

        [Command("rehash", RunMode = RunMode.Async), Summary("Rehash settings file")]
        public async Task Rehash()
        {
            try
            {
                if(!await IsExecByAdmin()) return;

                await SettingsManager.UpdateSettings();
                await SimplifiedAuth.UpdateInjectedSettings();
                TickManager.InvalidateModules();
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, ":white_check_mark: REHASH COMPLETED", true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Rehash command error", ex);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, ":no_entry_sign: REHASH FAILED :no_entry_sign: Check your config file for errors! (https://jsonformatter.org/)", true);
            }
        }

        [Command("reauth", RunMode = RunMode.Async), Summary("Reauth all users")]
        public async Task Reauth()
        {
            try
            {
                if(!await IsExecByAdmin()) return;

                await TickManager.RunModule(typeof(AuthCheckModule), Context, true);
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, "Reauth completed!", true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("reauth", ex);
            }
        }

        [Command("auth", RunMode = RunMode.Async), Summary("Auth User")]
        public async Task Auth()
        {
            if (SettingsManager.Settings.WebAuthModule.AutoClearAuthCommandsFromDiscord)
                await APIHelper.DiscordAPI.RemoveMessage(Context.Message);

            if(!IsAuthAllowed()) return;

            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                try
                {
                    var authString = ServerPaths.GetAuthPageUrl();
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
        [Command("auth", RunMode = RunMode.Async), Summary("Auth User")]
        public async Task Auth([Remainder] string x)
        {
            if (SettingsManager.Settings.WebAuthModule.AutoClearAuthCommandsFromDiscord)
                await APIHelper.DiscordAPI.RemoveMessage(Context.Message);

            if(!IsAuthAllowed())
                return;

            if (SettingsManager.Settings.Config.ModuleAuthWeb)
            {
                try
                {
                    if (!string.IsNullOrEmpty(x))
                    {
                        switch (x)
                        {
                            case "accept":
                                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authAcceptHelp"));
                                return;
                        }

                    }

                    if (APIHelper.DiscordAPI.IsUserMention(Context))
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("authInvite"), true);
                    }
                    else
                    {
                        await WebAuthModule.AuthUser(Context, x, 0, SettingsManager.Settings.Config.DiscordGuildId);
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
            if(IsForbidden()) return;

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
            if(IsForbidden()) return;
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
            if(IsForbidden()) return;
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
            if(IsForbidden()) return;
            try
            {
                if(SettingsManager.Settings.Config.ModuleStats && SettingsManager.Settings.StatsModule.EnableStatsCommand)
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpStat", SettingsManager.Settings.Config.BotDiscordCommandPrefix));
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
            }
        }

        [Command("stats", RunMode = RunMode.Async), Summary("Ally status")]
        public async Task Stats2()
        {
            if(IsForbidden()) return;

            try
            {
               // await ContinuousCheckModule.Stats(Context, "m");
                if(SettingsManager.Settings.Config.ModuleStats && SettingsManager.Settings.StatsModule.EnableStatsCommand)
                    await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpStat", SettingsManager.Settings.Config.BotDiscordCommandPrefix));

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("stat", ex);
            }
        }

        [Command("ships", RunMode = RunMode.Async), Summary("")]
        public async Task Ships()
        {
            if(IsForbidden()) return;
            if (!SettingsManager.Settings.CommandsConfig.EnableShipsCommand) 
                return;
            if(SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordChannels.Any() && !SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordChannels.Contains(Context.Channel.Id))
                return;
            if (SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordRoles.Any() && !await IsAllowedByRoles(SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordRoles, Context.User.Id))
                return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("helpShips", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "ships"));
        }

        [Command("ships", RunMode = RunMode.Async), Summary("")]
        public async Task Ships([Remainder] string x)
        {
            if(IsForbidden()) return;

            if (!SettingsManager.Settings.CommandsConfig.EnableShipsCommand) 
                return;
            if(SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordChannels.Any() && !SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordChannels.Contains(Context.Channel.Id))
                return;
            if (SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordRoles.Any() && !await IsAllowedByRoles(SettingsManager.Settings.CommandsConfig.ShipsCommandDiscordRoles, Context.User.Id))
                return;

            try
            {
                await TickManager.GetModule<ShipsModule>().ProcessWhoCommand(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("ships", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("motd", RunMode = RunMode.Async), Summary("Shows MOTD")]
        public async Task Motd()
        {
            if(IsForbidden()) return;

            if (SettingsManager.Settings.Config.ModuleMOTD)
            {
                try
                {
                   // await Functions.MOTD(Context);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("motd", ex);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
      /*  [Command("ops", RunMode = RunMode.Async), Summary("Shows current Fleetup Operations")]
        public async Task Ops()
        {
            if(IsForbidden()) return;
            if (SettingsManager.Settings.Config.ModuleFleetup) return;
            try
            {
                await FleetUpModule.Ops(Context, null);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("ops", ex);
            }
        }*/
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
       /* [Command("ops", RunMode = RunMode.Async), Summary("Shows current Fleetup Operations")]
        public async Task Ops([Remainder] string x)
        {
            if(IsForbidden()) return;
            if (SettingsManager.Settings.Config.ModuleFleetup) return;
            try
            {
                await FleetUpModule.Ops(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("ops", ex);
            }
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Command("about", RunMode = RunMode.Async), Summary("About the bot")]
        public async Task About()
        {
            if(IsForbidden()) return;

            try
            {
                await AboutModule.About(Context);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("about", ex);
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
                if(IsForbidden()) return;
                if (SettingsManager.Settings.Config.ModuleCharCorp)
                   await CharSearchModule.SearchCharacter(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("char", ex);
            }
        }

        [Command("corp", RunMode = RunMode.Async), Summary("")]
        public async Task Corp()
        {
            if (!SettingsManager.Settings.Config.ModuleFWStats) return;
            if(IsForbidden()) return;
            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("badstandHelp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "badstand"));
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
                if(IsForbidden()) return;
                if (SettingsManager.Settings.Config.ModuleCharCorp)
                  await CorpSearchModule.CorpSearch(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("corp", ex);
            }
        }

        [Command("badstand", RunMode = RunMode.Async), Summary("")]
        public async Task BadStandCommand()
        {
            if (!SettingsManager.Settings.Config.ModuleFWStats) return;
            if(IsForbidden()) return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("badstandHelp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "badstand"));
        }

        [Command("bs", RunMode = RunMode.Async), Summary("")]
        public async Task BadStandCommand2([Remainder] string x)
        {
            await BadStandCommand(x);
        }

        [Command("bs", RunMode = RunMode.Async), Summary("")]
        public async Task BadStandCommand2()
        {
            await BadStandCommand();
        }

        [Command("badstand", RunMode = RunMode.Async), Summary("")]
        public async Task BadStandCommand([Remainder] string x)
        {
            try
            {
                if (!SettingsManager.Settings.Config.ModuleFWStats) return;
                if(IsForbidden()) return;
                await FWStatsModule.DisplayBadStandings(Context, x);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("badstand", ex);
            }
        }

        #region Internal

        private async Task<bool> IsExecByAdmin()
        {
            //restrict admin commands on secondary guilds if nesessary
            if (Context.Guild.Id != SettingsManager.Settings.Config.DiscordGuildId &&
                !SettingsManager.Settings.Config.DiscordAllowSystemCommandsOnSecondaryGuilds) return false;

            var result = await APIHelper.DiscordAPI.IsAdminAccess(Context);
            if (!string.IsNullOrEmpty(result))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, result, true);
                return false;
            }

            return true;
        }

        private async Task<bool> IsAllowedByRoles(List<string> roles, ulong userId)
        {
            if (roles == null || !roles.Any()) return true;

            var result = APIHelper.DiscordAPI.GetUserRoleNames(userId);
            if (!result.Any(roles.Contains))
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(Context, LM.Get("comRequireRole"), true);
                return false;
            }

            return true;
        }

        private bool IsForbidden()
        {
            //restrict admin commands on secondary guilds if nesessary
            if (Context.Guild.Id != SettingsManager.Settings.Config.DiscordGuildId &&
                !SettingsManager.Settings.Config.DiscordAllowGeneralCommandsOnSecondaryGuilds) return true;

            var allowed = APIHelper.DiscordAPI.GetConfigAllowedPublicChannels();
            if (allowed.Any())
            {
               return !allowed.Contains(Context.Channel.Id);
            }

            var forbidden = APIHelper.DiscordAPI.GetConfigForbiddenPublicChannels();
            return forbidden.Any() && forbidden.Contains(Context.Channel.Id);
        }

        private bool IsAuthAllowed()
        {
            var channels = APIHelper.DiscordAPI.GetAuthAllowedChannels();
            return (channels.Length == 0 && !IsForbidden()) || channels.Contains(Context.Channel.Id);
        }

        #endregion

        #region Easters

        private static readonly List<string> VodkaMessages = new List<string>
        {
            "Here you go pal! Grab your :champagne:! What do you mean this is not VODKA?!...",
            "Fifte-e-e-e-en ma-a-a-a-an and a bo-o-o-o-ttle of... VODKA!",
            "Have you seen Tricia recently? Here, pass him his bottle of... VODKA!",
            "Don't you dare, boy? Sure?! Okay, here comes the.. VODKA!",
            "It doesn't matter how far into space you have stranded in your piece of junk boy. Just make sure you always have a bottle with ya. A bottle of... VODKA!",
            "Smirnoff VODKA Cerebral Implant: +5 to SP degradation and joy generation skills!",
            "Grab your glass boys, here comes the... VODKA!",
            "Do you know why LSH still holds in Aridia? That's right... VODKA!",
            "Do you know what's Quafe made of? Now you know boy, now you know...",
        };

        [Command("vodka", RunMode = RunMode.Async), Summary("")]
        public async Task VodkaCommand()
        {
            if(IsForbidden()) return;

            await APIHelper.DiscordAPI.ReplyMessageAsync(Context, VodkaMessages[IRC.Helpers.Helpers.Random(0, VodkaMessages.Count-1)], true);
        }
        #endregion
    }
}
