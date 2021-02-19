using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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
        public MiscUserData MiscData = new MiscUserData();
        public DateTime CreateDate;
        public DateTime? DumpDate;
        public string RegCode;
        public long? MainCharacterId;
        public DateTime? LastCheck;
        public string Ip;


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
            DumpDate = null;
        }

        public void SetStateAuthed()
        {
            AuthState = 2;
            DumpDate = null;
        }
    }

    public class MiscUserData
    {
        public DateTime BirthDate;
        public float? SecurityStatus;
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
