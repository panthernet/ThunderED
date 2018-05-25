using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.Settings;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;

        public AuthSettings Settings { get; }

        public AuthCheckModule()
        {
            Settings = AuthSettings.Load(SettingsManager.FileSettingsPath);
        }
   
        public async Task AuthCheck(bool? manual = false)
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                manual = manual ?? false;
                //Check inactive users are correct
                if (DateTime.Now > _lastAuthCheck.AddMinutes(Settings.Core.AuthCheckIntervalMinutes) || manual.Value)
                {
                    _lastAuthCheck = DateTime.Now;

                    await LogHelper.LogInfo("Running Auth Check", Category);

                    var foundList = new Dictionary<int, List<string>>();
                    foreach (var group in Settings.Core.AuthGroups.Values)
                    {
                        if (group.CorpID != 0)
                            foundList.Add(group.CorpID, group.MemberRoles);
                        if (group.AllianceID != 0)
                            foundList.Add(group.AllianceID, group.MemberRoles);
                    }

                    await APIHelper.DiscordAPI.UpdateAllUserRoles(foundList, Settings.Core.ExemptDiscordRoles);
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
