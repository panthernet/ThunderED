using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        private DateTime _lastDiscordAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            IsRunning = true;
            var manual = (bool?) prm ?? false;

            try
            {
                await CheckDiscordUsers(manual);
                await CheckDBUsers(manual);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task CheckDBUsers(bool manual)
        {
            //Check inactive users are correct
            if (DateTime.Now > _lastAuthCheck.AddMinutes(2) || manual)
            {
                _lastAuthCheck = DateTime.Now;

                await LogHelper.LogModule("Running DB users auth check...", Category);
                await WebAuthModule.UpdateAllUserRoles(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles);
            }

        }

        private async Task CheckDiscordUsers(bool manual)
        {
            if (DateTime.Now > _lastDiscordAuthCheck.AddMinutes(15) || manual)
            {
                _lastDiscordAuthCheck = DateTime.Now;
                await LogHelper.LogModule("Running Discord users auth check...", Category);
                await WebAuthModule.UpdateAuthUserRolesFromDiscord(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles);
            }
        }
    }
}
