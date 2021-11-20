using System.Collections.Generic;
using System.Threading.Tasks;

using ThunderED.Helpers;

namespace ThunderED
{
    public static class DiscordHelper
    {
        public static async Task<List<string>> GetDiscordRoles(long characterId)
        {
            var user = await DbHelper.GetAuthUser(characterId);
            if (user == null || !user.DiscordId.HasValue) return null;
            return APIHelper.DiscordAPI.GetUserRoleNames(user.DiscordId.Value);
        }
    }
}
