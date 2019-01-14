using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ThunderED.Json;

namespace ThunderED.Classes.Entities
{
    public class AuthUserEntity
    {
        public long Id;
        public long CharacterId;
        public ulong DiscordId;
        public string GroupName;
        public string RefreshToken;
        public int AuthState;
        public AuthUserData Data = new AuthUserData();

        //for compatibility
        public string EveName;
        public string Group;
        public bool IsActive;

        [JsonIgnore]
        public bool IsAuthed => AuthState == 2;
        [JsonIgnore]
        public bool IsPending => AuthState == 1;

        [JsonIgnore]
        public bool HasToken => !string.IsNullOrEmpty(RefreshToken);
    }

    public class AuthUserData
    {
        public string CharacterName;
        public string CorporationName;
        public string AllianceName;
        public string Permissions;
        public long CorporationId;
        public long AllianceId;
        public string CorporationTicker;
        public string AllianceTicker;

        [JsonIgnore]
        public List<string> PermissionsList => string.IsNullOrEmpty(Permissions) ? new List<string>() : Permissions.Split(',').ToList();

        public void Update(long corpId, JsonClasses.CorporationData corp, JsonClasses.AllianceData alliance)
        {
            if (corp != null)
            {
                CorporationName = corp.name;
                CorporationTicker = corp.ticker;
                CorporationId = corpId;
            }

            if (alliance != null)
            {
                AllianceName = alliance.name;
                AllianceTicker = alliance.ticker;
                AllianceId = corp?.alliance_id ?? 0;
            }
        }
    }
}
