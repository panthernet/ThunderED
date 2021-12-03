using System.ComponentModel.DataAnnotations.Schema;
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

            public bool IsUnreachable()
            {
                return IsWormhole() || IsAbyss();
            }

            public bool IsWormhole()
            {
                return !string.IsNullOrEmpty(name) && (system_id >= 31000000 && system_id <= 32000000);
            }

            public bool IsThera()
            {
                return !string.IsNullOrEmpty(name) && system_id == 31000005;
            }

            public bool IsAbyss()
            {
                return !string.IsNullOrEmpty(name) && (system_id >= 32000000 && system_id <= 33000000);
            }

        }
    }
}
 