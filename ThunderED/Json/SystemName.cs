using Newtonsoft.Json;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class SystemName
        {
            public int constellation_id { get; set; }
            public string name { get; set; }
            public Planet[] planets { get; set; }
            public Position position { get; set; }
            public string security_class;
            public float security_status { get; set; }
            public int star_id { get; set; }
            public int[] stargates { get; set; }
            public int system_id { get; set; }

            [JsonIgnore]
            public int? DB_RegionId;
        }
    }
}