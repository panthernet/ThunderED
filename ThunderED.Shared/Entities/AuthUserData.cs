using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ThunderED.Classes.Entities
{
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