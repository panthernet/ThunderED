using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public class DiscordRolesManagementModule: AppModuleBase
    {
        private static LogCat StaticCategory => LogCat.DiscordRoles;
        public override LogCat Category => LogCat.DiscordRoles;

        internal static List<string> AvailableRoleNames { get; set; } = new List<string>();
        internal static List<string> AvailableAllowedRoleNames { get; set; } = new List<string>();

        public override async Task Initialize()
        {
            var missing = await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.CommandsConfig.RolesCommandDiscordRoles, Category);
            if (missing.Any())
            {
                foreach (var role in Settings.CommandsConfig.RolesCommandDiscordRoles)
                {
                    if(missing.ContainsCaseInsensitive(role)) continue;
                    AvailableRoleNames.Add(role);
                }
            }else AvailableRoleNames = Settings.CommandsConfig.RolesCommandDiscordRoles;


            missing = await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.CommandsConfig.RolesCommandAllowedRoles, Category);
            if (missing.Any())
            {
                foreach (var role in Settings.CommandsConfig.RolesCommandAllowedRoles)
                {
                    if(missing.ContainsCaseInsensitive(role)) continue;
                    AvailableAllowedRoleNames.Add(role);
                }
            }else AvailableAllowedRoleNames = Settings.CommandsConfig.RolesCommandAllowedRoles;

            
        }

        public static async Task AddRole(ICommandContext context, string role)
        {
            try
            {
                var groups = APIHelper.DiscordAPI.GetGuildRoleNames();
                if(!groups.Any()) return;
                var validGroups = !SettingsManager.Settings.CommandsConfig.RolesCommandDiscordRoles.Any() ? groups : AvailableRoleNames;

                if (!validGroups.ContainsCaseInsensitive(role))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsRoleNotFound", role), true);
                    return;
                }

                if(!IsUserAllowed(context.Message.Author.Id))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsAccessDenied"), true);
                    return;
                }

                if (await APIHelper.DiscordAPI.AssignRoleToUser(context.Message.Author.Id, role))
                {
                    var message = LM.Get("roleCommandsRoleAdded", role);
                    await LogHelper.LogInfo(message, StaticCategory);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, message, true);
                }
                else await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsRoleNotAdded", role), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(AddRole), ex, StaticCategory);
            }
        }

        public static async Task RemoveRole(ICommandContext context, string role)
        {
            try
            {
                var groups = APIHelper.DiscordAPI.GetGuildRoleNames();
                if(!groups.Any()) return;
                var validGroups = APIHelper.DiscordAPI.GetUserRoleNames(context.Message.Author.Id);
                if (!validGroups.ContainsCaseInsensitive(role))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsDontHaveRole", role), true);
                    return;
                }

                if(!IsUserAllowed(context.Message.Author.Id))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsAccessDenied"), true);
                    return;
                }

                if (await APIHelper.DiscordAPI.StripUserRole(context.Message.Author.Id, role))
                {
                    var message = LM.Get("roleCommandsRoleRemoved", role);
                    await LogHelper.LogInfo(message, StaticCategory);
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, message, true);
                }
                else await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsRoleNotRemoved", role), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(RemoveRole), ex, StaticCategory);
            }
        }

        public static async Task ListRoles(ICommandContext context)
        {
            try
            {
                if(!IsUserAllowed(context.Message.Author.Id))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsAccessDenied"), true);
                    return;
                }

                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsListRoles", string.Join(", ", AvailableRoleNames), SettingsManager.Settings.Config.BotDiscordCommandPrefix, DiscordCommands.CMD_ADDROLE), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(RemoveRole), ex, StaticCategory);
            }
        }

        private static bool IsUserAllowed(ulong id)
        {
            var userRoles = APIHelper.DiscordAPI.GetUserRoleNames(id);
            return !SettingsManager.Settings.CommandsConfig.RolesCommandAllowedRoles.Any() || AvailableAllowedRoleNames.Intersect(userRoles).Any();
        }
    }
}
