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
            public int ceo_id { get; set; }
            public float tax_rate { get; set; }
            public int creator_id { get; set; }
            public int? alliance_id { get; set; }
            public string description { get; set; }
            public DateTime date_founded { get; set; }
            public string url { get; set; }
            public int? home_station_id { get; set; }
            public Int64? shares { get; set; }
            public int? faction_id { get; set; }
        }
    }
}