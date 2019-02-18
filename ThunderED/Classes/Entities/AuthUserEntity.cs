using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Helpers;
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
        public DateTime CreateDate { get; set; }
        public DateTime? DumpDate { get; set; }
        public string RegCode { get; set; }


        //for compatibility
        [Obsolete("Maintained for upgrade possibility")]
        public string EveName;
        [Obsolete("Maintained for upgrade possibility")]
        public string Group;
        [Obsolete("Maintained for upgrade possibility")]
        public bool IsActive;

        [JsonIgnore]
        public bool IsDumped => AuthState == 3;

        [JsonIgnore]
        public bool IsAuthed => AuthState == 2;
        [JsonIgnore]
        public bool IsPending => AuthState < 2;
        [JsonIgnore]
        public bool IsSpying => AuthState == 4;

        [JsonIgnore]
        public bool HasToken => !string.IsNullOrEmpty(RefreshToken);

        [JsonIgnore]
        public bool HasRegCode => !string.IsNullOrEmpty(RegCode);

        public void SetStateDumpster()
        {
            AuthState = 3;
            DumpDate = DateTime.Now;
        }

        public void SetStateSpying()
        {
            AuthState = 4;
            DumpDate = null;
        }

        public void SetStateAwaiting()
        {
            AuthState = 1;
        }

        public void SetStateAuthed()
        {
            AuthState = 2;
        }

        public async void UpdateData(JsonClasses.CharacterData characterData, JsonClasses.CorporationData rCorp = null)
        {
            rCorp = rCorp ?? await APIHelper.ESIAPI.GetCorporationData(LogCat.AuthCheck.ToString(), characterData.corporation_id, true);
            Data.CorporationId = characterData.corporation_id;
            Data.CorporationName = rCorp.name;
            Data.CorporationTicker = rCorp.ticker;
            Data.AllianceId = characterData.alliance_id ?? 0;
            if (Data.AllianceId > 0)
            {
                var rAlliance = await APIHelper.ESIAPI.GetAllianceData(LogCat.AuthCheck.ToString(), characterData.alliance_id, true);
                Data.AllianceName = rAlliance.name;
                Data.AllianceTicker = rAlliance.ticker;
            }
        }


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

        public async Task Update(long characterId, string permissions = null)
        {
            var ch = await APIHelper.ESIAPI.GetCharacterData("Auth", characterId);
            if(ch == null) return;
            CharacterName = ch.name;
            var rCorp = await APIHelper.ESIAPI.GetCorporationData("AuthUser", ch.corporation_id, true);
            CorporationId = ch.corporation_id;
            CorporationName = rCorp.name;
            CorporationTicker = rCorp.ticker;
            AllianceId = ch.alliance_id ?? 0;
            if (AllianceId > 0)
            {
                var rAlliance = await APIHelper.ESIAPI.GetAllianceData("AuthUser", ch.alliance_id, true);
                AllianceName = rAlliance.name;
                AllianceTicker = rAlliance.ticker;
            }

            if (permissions != null)
                Permissions = permissions;
        }
    }
}
