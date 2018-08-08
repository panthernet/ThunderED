using System;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class AllianceData
        {
            public string name { get; set; }
            public string ticker { get; set; }
            public int creator_id { get; set; }
            public int creator_corporation_id { get; set; }
            public int executor_corporation_id { get; set; }
            public DateTime date_founded { get; set; }
            public int? faction_id { get; set; }
        }

        internal class AllianceIDLookup
        {
            public int[] alliance;
        }

        internal class FactionData
        {
            public long corporation_id;
            public string description;
            public long faction_id;
            public bool is_unique;
            public string name;
            public double size_factor;
            public long solar_system_id;
            public int station_count;
            public int station_system_count;
        }
    }
}