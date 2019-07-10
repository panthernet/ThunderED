using System;
using System.Collections.Generic;
using System.Linq;
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
        public DateTime CreateDate;
        public DateTime? DumpDate;
        public string RegCode;
        public long? MainCharacterId;


        //for compatibility
     /*   [Obsolete("Maintained for upgrade possibility")]
        public string EveName;
        [Obsolete("Maintained for upgrade possibility")]
        public string Group;
        [Obsolete("Maintained for upgrade possibility")]
        public bool IsActive;*/

        [JsonIgnore]
        public bool IsDumped => AuthState == 3;

        [JsonIgnore]
        public bool IsAuthed => AuthState == 2;
        [JsonIgnore]
        public bool IsPending => AuthState < 2;
        [JsonIgnore]
        public bool IsSpying => AuthState == 4;

        [JsonIgnore]
        public bool IsAltChar => MainCharacterId > 0;

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

        public async Task UpdateData(string permissions = null)
        {
            var ch = await APIHelper.ESIAPI.GetCharacterData(LogCat.AuthCheck.ToString(), CharacterId, true);
            if(ch == null) return;
            await UpdateData(ch, null, null, permissions);
        }

        public async Task UpdateData(JsonClasses.CharacterData characterData, JsonClasses.CorporationData rCorp = null, JsonClasses.AllianceData rAlliance = null, string permissions = null)
        {
            rCorp = rCorp ?? await APIHelper.ESIAPI.GetCorporationData(LogCat.AuthCheck.ToString(), characterData.corporation_id, true);
            Data.CharacterName = characterData.name;
            Data.CorporationId = characterData.corporation_id;
            Data.CorporationName = rCorp?.name;
            Data.CorporationTicker = rCorp?.ticker;
            Data.AllianceId = characterData.alliance_id ?? 0;
            if (Data.AllianceId > 0)
            {
                rAlliance = rAlliance ?? await APIHelper.ESIAPI.GetAllianceData(LogCat.AuthCheck.ToString(), characterData.alliance_id, true);
                Data.AllianceName = rAlliance?.name;
                Data.AllianceTicker = rAlliance?.ticker;
            }
            if (permissions != null)
                Data.Permissions = permissions;
        }

        public static async Task<AuthUserEntity> CreateAlt(long characterId, string refreshToken, WebAuthGroup @group, string groupName, long mainCharId)
        {
            var authUser = new AuthUserEntity
            {
                CharacterId = characterId,
                DiscordId = 0,
                RefreshToken = refreshToken,
                GroupName = groupName,
                AuthState = 2,
                CreateDate = DateTime.Now,
                MainCharacterId = mainCharId
            };
            await authUser.UpdateData(group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null);
            return authUser;
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
        public long LastSpyMailId;

        [JsonIgnore]
        public List<string> PermissionsList => string.IsNullOrEmpty(Permissions) ? new List<string>() : Permissions.Split(',').ToList();
    }
}
