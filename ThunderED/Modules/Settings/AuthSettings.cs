using System.Collections.Generic;
using ThunderED.Classes;

namespace ThunderED.Modules.Settings
{
    public class AuthSettings: SettingsBase<AuthSettings>
    {
        public AuthSettingsInternal Core => Auth;
        public AuthSettingsInternal Auth;
    }

    public class AuthSettingsInternal
    {
        public int AuthCheckIntervalMinutes = 30;
        public string DiscordUrl;
        public string CcpAppClientId;
        public string CcpAppSecret;
        public List<string> ExemptDiscordRoles = new List<string>();
        public ulong AuthReportChannel;
        public List<ulong> ComAuthChannels = new List<ulong>();
        public bool EnforceCorpTickers;
        public bool EnforceCharName;
        public Dictionary<string, AuthGroup> AuthGroups = new Dictionary<string, AuthGroup>();
    }

    public class AuthGroup
    {
        public int CorpID;
        public int AllianceID;
        public List<string> MemberRoles;
    }
}
