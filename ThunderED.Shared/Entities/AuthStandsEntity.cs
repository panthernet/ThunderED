using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Classes.Entities
{
    public class AuthStandsEntity
    {
        public long CharacterID { get; set; }
        public string Token { get; set; }
        public List<JsonClasses.Contact> PersonalStands { get; set; }
        public List<JsonClasses.Contact> CorpStands { get; set; }
        public List<JsonClasses.Contact> AllianceStands { get; set; }
    }
}
