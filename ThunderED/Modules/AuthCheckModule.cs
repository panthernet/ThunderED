using System;
using System.Collections.Generic;
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

                    var foundList = new Dictionary<int, List<string>>();
                    foreach (var group in Settings.WebAuthModule.AuthGroups.Values)
                    {
                        if (group.CorpIDList.Count > 0)
                            group.CorpIDList.ForEach(c => foundList.Add(c, group.MemberRoles));
                        if (group.AllianceIDList.Count > 0)
                            group.AllianceIDList.ForEach(a => foundList.Add(a, group.MemberRoles));
                    }

                    await APIHelper.DiscordAPI.UpdateAllUserRoles(foundList, Settings.WebAuthModule.ExemptDiscordRoles, Settings.WebAuthModule.AuthCheckIgnoreRoles);
                    await LogHelper.LogInfo("Auth check complete!", Category);
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
