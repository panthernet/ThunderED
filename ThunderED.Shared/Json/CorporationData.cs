using System;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class CorporationData
        {
            public string name { get; set; }
            public string ticker { get; set; }
            public int member_count { get; set; }
            public long ceo_id { get; set; }
            public float tax_rate { get; set; }
            public long creator_id { get; set; }
            public long? alliance_id { get; set; }
            public string description { get; set; }
            public DateTime date_founded { get; set; }
            public string url { get; set; }
            public long? home_station_id { get; set; }
            public long? shares { get; set; }
            public long? faction_id { get; set; }
        }
    }
}