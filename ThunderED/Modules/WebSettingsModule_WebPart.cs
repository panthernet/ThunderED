using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public partial class WebSettingsModule
    {
        public async Task<WCEAccessFilter> GetAccess(long userId)
        {
            if (TickManager.IsESIUnreachable || TickManager.IsNoConnection) return new WCEAccessFilter();
            return await CheckAccess(userId, await APIHelper.ESIAPI.GetCharacterData("Web", userId, true));
        }

        public async Task WebSaveSimplifiedAuth(List<SimplifiedAuthEntity> list)
        {
            //update Roles string
            list.ForEach(a=> a.Roles = string.Join(',', a.RolesList));
            //save data
            await SimplifiedAuth.SaveData(list
                .Select(a => $"{a.Name}|{a.Group}|{a.Roles}").ToList());
            //inject updated simplified auth data
            await SimplifiedAuth.LoadData();
            //rebuild auth cache
            if (Settings.Config.ModuleAuthWeb)
                await TickManager.GetModule<WebAuthModule>().Initialize();
            await LogHelper.LogInfo("Simplified auth update completed!", Category);
        }

        public List<string> WebGetAuthGroupsList()
        {
            return Settings.WebAuthModule.AuthGroups.Keys.ToList();
        }

        public List<string> WebGetAuthRolesList()
        {
            var list = APIHelper.DiscordAPI.GetGuildRoleNames(Settings.Config.DiscordGuildId)
                ?.Where(a => !a.StartsWith("@")).ToList();
            return list;
        }

        public TiData CreateNewTimersData(List<TiData> list)
        {
            return new TiData {Id = list.Any() ? (list.Max(a => a.Id) + 1) : 1};
        }

        public List<TiData> WebGetTimersAccessList()
        {
            var counter = 0;
            var list =  SettingsManager.Settings.TimersModule.AccessList.Select(a => new TiData
            {
                Id = ++counter,
                Name = a.Key,
                Entities = string.Join(",", a.Value.FilterEntities.Select(b => b.ToString())),
                Roles = string.Join(",", a.Value.FilterDiscordRoles),
                RolesList = a.Value.FilterDiscordRoles
            }).ToList();

            return list;
        }

        public List<TiData> WebGetTimersEditList()
        {
            var counter = 0;
            var list = SettingsManager.Settings.TimersModule.EditList.Select(a => new TiData
            {
                Id = ++counter,
                Name = a.Key,
                Entities = string.Join(",", a.Value.FilterEntities.Select(b => b.ToString())),
                Roles = string.Join(",", a.Value.FilterDiscordRoles),
                RolesList = a.Value.FilterDiscordRoles
            }).ToList();

            return list;
        }

        public async Task WebSaveTimersAccess(List<TiData> timersList)
        {
            await SaveTimersAuthData(timersList, Settings.TimersModule.AccessList);
        }

        public async Task WebSaveTimersEdit(List<TiData> timersList)
        {
            await SaveTimersAuthData(timersList, Settings.TimersModule.EditList);
        }

    }
}
