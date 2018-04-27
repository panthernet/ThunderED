using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace ThunderED.Classes
{
    public class CheckForRoleAttribute : PreconditionAttribute
    {
        // Override the CheckPermissions method
        public override async Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider map)
        {
            var roles = new List<IRole>(context.Guild.Roles);
            var userRoleIDs = context.Guild.GetUserAsync(context.User.Id).Result.RoleIds;
            var roleMatch = SettingsManager.GetSubList("config","discordAdminRoles");
            if ((from role in roleMatch select roles.FirstOrDefault(x => x.Name == role.Value) into tmp where tmp != null select userRoleIDs.FirstOrDefault(x => x == tmp.Id))
                .All(check => check == 0)) return PreconditionResult.FromError(LM.Get("comRequirePriv"));
            await Task.CompletedTask;
            return PreconditionResult.FromSuccess();
        }
    }
}
