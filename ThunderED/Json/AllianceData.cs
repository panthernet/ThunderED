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
    }
}