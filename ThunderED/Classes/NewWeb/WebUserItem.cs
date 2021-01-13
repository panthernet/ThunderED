using System;

namespace ThunderED.Classes
{
    [Serializable]
    public class WebUserItem
    {
        public long Id { get; set; }
        public string CharacterName { get; set; }
        public string CorporationName { get; set; }
        public string AllianceName { get; set; }
        public string CorporationTicker { get; set; }
        public string AllianceTicker { get; set; }
        public string IconUrl { get; set; }
        public DateTime RegDate { get; set; }
    }
}
