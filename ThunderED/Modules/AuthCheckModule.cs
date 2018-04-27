using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class AuthCheckModule: AppModuleBase
    {
        private DateTime _lastAuthCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthCheck;
   
        public async Task AuthCheck(bool? manual = false)
        {
            manual = manual ?? false;
            //Check inactive users are correct
            if (DateTime.Now > _lastAuthCheck.AddMinutes(SettingsManager.GetInt("auth", "authCheckIntervalMinutes")) || manual.Value)
            {
                _lastAuthCheck = DateTime.Now;

                await LogHelper.LogInfo("Running Auth Check", Category);

                var authgroups = SettingsManager.GetSubList("auth","authgroups");
                var exemptRoles = SettingsManager.GetSubList("auth","exemptDiscordRoles").ToArray();
                var guildID = SettingsManager.GetULong("config", "discordGuildId");
               // var logchan = Convert.ToUInt64(Program.Settings.GetSection("auth")["alertChannel"]);

                var corps = new Dictionary<string, string>();
                var alliance = new Dictionary<string, string>();

                foreach (var config in authgroups)
                {
                    var configChildren = config.GetChildren().ToList();

                    var corpID = configChildren.FirstOrDefault(x => x.Key == "corpID")?.Value ?? "";
                    var allianceID = configChildren.FirstOrDefault(x => x.Key == "allianceID")?.Value ?? "";
                    var corpMemberRole = configChildren.FirstOrDefault(x => x.Key == "corpMemberRole")?.Value ?? "";
                    var allianceMemberRole = configChildren.FirstOrDefault(x => x.Key == "allianceMemberRole")?.Value ?? "";

                    if (Convert.ToInt32(corpID) != 0)
                        corps.Add(corpID, corpMemberRole);
                    if (Convert.ToInt32(allianceID) != 0)
                        alliance.Add(allianceID, allianceMemberRole);
                }

                await APIHelper.DiscordAPI.UpdateAllUserRoles(guildID, alliance, corps, exemptRoles);
                await LogHelper.LogInfo("Auth check complete!", Category);
            }
        }

        public override async Task Run(object prm)
        {
            await AuthCheck((bool?)prm);
        }
    }
}
