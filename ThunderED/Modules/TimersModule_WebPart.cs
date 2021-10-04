using System;
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

        public static bool HasWebAccess(long id, long corpId, long allianceId)
        {
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            if (module == null) return false;
            return module.GetAllCharacterIds().Contains(id) || module.GetAllCorporationIds().Contains(corpId) ||
                   module.GetAllAllianceIds().Contains(allianceId);
        }

        public static bool HasWebEditorAccess(in long userId, in long corpId, in long allyId)
        {
            //todo discord roles check
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            return module.GetAllParsedCharacters(module.ParsedEditLists).Contains(userId) ||
                   module.GetAllParsedCorporations(module.ParsedEditLists).Contains(corpId) ||
                   module.GetAllParsedAlliances(module.ParsedEditLists).Contains(allyId);
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
