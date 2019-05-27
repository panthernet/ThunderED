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

        public override async Task Initialize()
        {
            var groups = APIHelper.DiscordAPI.GetGuildRoleNames();
            if(!groups.Any()) return;
            if(!Settings.CommandsConfig.RolesCommandDiscordRoles.Any()) return;

            var missing = Settings.CommandsConfig.RolesCommandDiscordRoles.Except(groups).ToList();
            if (missing.Any())
            {
                await LogHelper.LogWarning(LM.Get("roleCommandsUnknownRoles", string.Join(',', missing)), Category);
                foreach (var role in Settings.CommandsConfig.RolesCommandDiscordRoles)
                {
                    if(missing.ContainsCaseInsensitive(role)) continue;
                    AvailableRoleNames.Add(role);
                }
            }else AvailableRoleNames = Settings.CommandsConfig.RolesCommandDiscordRoles;
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
                 await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("roleCommandsListRoles", string.Join(", ", AvailableRoleNames), SettingsManager.Settings.Config.BotDiscordCommandPrefix, DiscordCommands.CMD_ADDROLE), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(RemoveRole), ex, StaticCategory);
            }
        }
    }
}
