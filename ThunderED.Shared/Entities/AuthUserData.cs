using System;

namespace ThunderED
{
    public class AuthUserData
    {
        [Obsolete]
        public string CharacterName;
        public string CorporationName;
        public string AllianceName;
        /// <summary>
        /// Stores ESI scopes from original registration. Just in case.
        /// </summary>
        public string Permissions;
        [Obsolete]
        public long CorporationId;
        [Obsolete]
        public long AllianceId;
        public string CorporationTicker;
        public string AllianceTicker;
        public long LastSpyMailId;

        //[Obsolete]
        //[JsonIgnore]
        //public List<string> PermissionsList => string.IsNullOrEmpty(Permissions) ? new List<string>() : Permissions.Split(',').ToList();
    }
}