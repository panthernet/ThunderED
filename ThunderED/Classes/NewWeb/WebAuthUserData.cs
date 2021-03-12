using System;
using ThunderED.Thd;

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

        public WebAuthUserData(ThdAuthUser user, string code)
        {
            Id = user.CharacterId;
            Name = user.DataView.CharacterName;
            CorpId = user.DataView.CorporationId;
            AllianceId = user.DataView.AllianceId;
            Code = code;
        }

    }
}
