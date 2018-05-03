using System;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class CharacterData
        {
            public string name { get; set; }
            public string description { get; set; }
            public int corporation_id { get; set; }
            public int? alliance_id { get; set; }
            public DateTime birthday { get; set; }
            public string gender { get; set; }
            public int race_id { get; set; }
            public int bloodline_id { get; set; }
            public int? ancestry_id { get; set; }
            public float? security_status { get; set; }
            public int? faction_id { get; set; }
        }
    }
}