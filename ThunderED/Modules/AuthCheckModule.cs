using System;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        private DateTime _lastDiscordAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Auth Check module...", Category);
        }

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            if(Settings.Config.ModuleAuthWeb && TickManager.GetModule<WebAuthModule>().IsEntityInitFailed) return;
            if(!Settings.Config.ModuleAuthCheck) return;

            IsRunning = true;
            var manual = (bool?) prm ?? false;

            try
            {
                if(Settings.WebAuthModule.AuthCheckUnregisteredDiscordUsers && APIHelper.IsDiscordAvailable)
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
                if (manual)
                {
                    await DbHelper.ResetAuthUsersLastCheck();
                }
                await WebAuthModule.UpdateAllUserRoles(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles, manual);
                await LogHelper.LogModule("DB users auth check complete!", Category);
            }

        }

        private async Task CheckDiscordUsers(bool manual)
        {
            if (DateTime.Now > _lastDiscordAuthCheck.AddMinutes(5) || manual)
            {
                _lastDiscordAuthCheck = DateTime.Now;
                await LogHelper.LogModule("Running Discord users auth check...", Category);
                await WebAuthModule.UpdateAuthUserRolesFromDiscord(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles, true);
            }
        }
    }
}
