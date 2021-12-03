using System;
using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdStandsAuth
    {
        public long CharacterId { get; set; }
        [Obsolete]
        public string Token { get; set; }
        public List<JsonClasses.Contact> PersonalStands { get; set; }
        public List<JsonClasses.Contact> CorpStands { get; set; }
        public List<JsonClasses.Contact> AllianceStands { get; set; }

    }
}
