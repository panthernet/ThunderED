using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public async Task AuthCheck(bool? manual = false)
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                manual = manual ?? false;
                //Check inactive users are correct
                if (DateTime.Now > _lastAuthCheck.AddMinutes(Settings.WebAuthModule.AuthCheckIntervalMinutes) || manual.Value)
                {
                    _lastAuthCheck = DateTime.Now;

                    await LogHelper.LogModule("Running AuthCheck module...", Category);

                    await APIHelper.DiscordAPI.UpdateAllUserRoles(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles);
                   // await LogHelper.LogInfo("Auth check complete!", Category);
                }
            }
            finally
            {
                IsRunning = false;
            }
        }

        public override async Task Run(object prm)
        {
            await AuthCheck((bool?)prm);
        }
    }
}
