using System;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public partial class TimersModule
    {
        private async Task WebPartInitialization()
        {

            await Task.CompletedTask;
        }

        public static async Task<bool> HasWebAccess(WebAuthUserData usr)
        {
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            if (module == null) return false;


            var result =  module.GetAllCharacterIds().Contains(usr.Id) || module.GetAllCorporationIds().Contains(usr.CorpId) ||
                   module.GetAllAllianceIds().Contains(usr.AllianceId);
            if (result) return true;

            var roles = await DiscordHelper.GetDiscordRoles(usr.Id);
            if (roles == null) return false;

            foreach (var group in SettingsManager.Settings.TimersModule.AccessList.Values)
            {
                if (group.FilterDiscordRoles != null && roles.Intersect(group.FilterDiscordRoles)
                    .Any())
                    return true;
            }

            return false;
        }

        public static async Task<bool> HasWebEditorAccess(WebAuthUserData usr)
        {
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            var result = module.GetAllParsedCharacters(module.ParsedEditLists).Contains(usr.Id) ||
                   module.GetAllParsedCorporations(module.ParsedEditLists).Contains(usr.CorpId) ||
                   module.GetAllParsedAlliances(module.ParsedEditLists).Contains(usr.AllianceId);
            if (result) return true;

            var roles = await DiscordHelper.GetDiscordRoles(usr.Id);
            if (roles == null || !roles.Any()) return false;
            foreach (var group in SettingsManager.Settings.TimersModule.AccessList.Values)
            {
                if (group.FilterDiscordRoles != null && roles.Intersect(group.FilterDiscordRoles)
                    .Any())
                    return true;
            }

            return false;
        }

        public static async Task<string> SaveTimer(WebTimerData data, WebAuthUserData user)
        {
            try
            {
                if (user == null || user.Id == 0) return null;

                var rChar = await APIHelper.ESIAPI.GetCharacterData(LogCat.Timers.ToString(), user.Id, true);
                if (rChar == null)
                    return LM.Get("webAuthenticationExpired");

                var module = TickManager.GetModule<TimersModule>();
                var checkResult = await module.CheckAccess(user.Id, rChar);
                if (!checkResult[0] || !checkResult[1])
                    return LM.Get("webAuthenticationExpired");

                var timer = data.FromWebTimerData(data, user);

                var iDate = timer.GetDateTime();
                if (iDate == null)
                    return LM.Get("invalidTimeFormat");

                if (iDate < DateTime.UtcNow)
                    return LM.Get("passedTimeValue");

                await SQLHelper.UpdateTimer(timer);
                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(SaveTimer), ex, LogCat.Timers);
                return LM.Get("webFatalError");
            }
        }

        public static async Task<string> SaveTimerRf(WebTimerDataRf data, WebAuthUserData user)
        {
            data.PushDate();
            return await SaveTimer(data, user);
        }
    }
}
