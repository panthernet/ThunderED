using Newtonsoft.Json;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class SystemName
        {
            public long constellation_id { get; set; }
            public string name { get; set; }
            public Planet[] planets { get; set; }
            public Position position { get; set; }
            public string security_class;
            public float security_status { get; set; }
            public long star_id { get; set; }
            public long[] stargates { get; set; }
            public long system_id { get; set; }

            [JsonIgnore]
            public long? DB_RegionId;
        }
    }
}