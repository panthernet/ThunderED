using System;
using ThunderED.Classes.Entities;

namespace ThunderED.Classes
{
    [Serializable]
    public class WebAuthUserData
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public long CorpId { get; set; }
        public long AllianceId { get; set; }

        public WebAuthUserData() { }

        public WebAuthUserData(AuthUserEntity user, string code)
        {
            Id = user.CharacterId;
            Name = user.Data.CharacterName;
            CorpId = user.Data.CorporationId;
            AllianceId = user.Data.AllianceId;
            Code = code;
        }

    }
}
