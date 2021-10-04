using System;
using Newtonsoft.Json;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class CharacterData
        {
            public string name { get; set; }
            public string description { get; set; }
            public long corporation_id { get; set; }
            public long? alliance_id { get; set; }
            public DateTime birthday { get; set; }
            public string gender { get; set; }
            public long race_id { get; set; }
            public long bloodline_id { get; set; }
            public long? ancestry_id { get; set; }
            public float? security_status { get; set; }
            public long? faction_id { get; set; }

            [JsonIgnore] 
            public long character_id;
        }
    }
}