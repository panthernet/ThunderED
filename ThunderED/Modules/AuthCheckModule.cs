using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            IsRunning = true;
            var manual = (bool?) prm;
            try
            {
                manual = manual ?? false;
                //Check inactive users are correct
                if (DateTime.Now > _lastAuthCheck.AddMinutes(Settings.WebAuthModule.AuthCheckIntervalMinutes) || manual.Value)
                {
                    _lastAuthCheck = DateTime.Now;

                    await LogHelper.LogModule("Running AuthCheck module...", Category);

                    var sw = Stopwatch.StartNew();
                    await APIHelper.DiscordAPI.UpdateAllUserRoles(Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles);
                    // await LogHelper.LogInfo("Auth check complete!", Category);
                    sw.Stop();
                    Debug.WriteLine(sw.Elapsed.TotalSeconds);
                }
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
