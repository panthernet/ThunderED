using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ThunderED
{
    public class AuthUserData
    {
        [Obsolete]
        public string CharacterName;
        public string CorporationName;
        public string AllianceName;
        [Obsolete]
        public string Permissions;
        [Obsolete]
        public long CorporationId;
        [Obsolete]
        public long AllianceId;
        public string CorporationTicker;
        public string AllianceTicker;
        public long LastSpyMailId;

        [Obsolete]
        [JsonIgnore]
        public List<string> PermissionsList => string.IsNullOrEmpty(Permissions) ? new List<string>() : Permissions.Split(',').ToList();
    }
}