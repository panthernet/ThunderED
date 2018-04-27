using System;

namespace ThunderED.Json.EveCentral
{
    internal partial class JsonEveCentral
    {
        //EVE Central

        public class EveCentralApi
        {
            public Items[] property1 { get; set; }
        }

        public class Items
        {
            public Buy buy { get; set; }
            public All all { get; set; }
            public Sell sell { get; set; }
        }

        public class Forquery
        {
            public bool Bid { get; set; }
            public int[] Types { get; set; }
            public object[] Regions { get; set; }
            public object[] Systems { get; set; }
            public int Hours { get; set; }
            public int Minq { get; set; }
        }

        public class All
        {
            public Forquery1 forQuery { get; set; }
            public int volume { get; set; }
            public float wavg { get; set; }
            public float avg { get; set; }
            public float variance { get; set; }
            public float stdDev { get; set; }
            public float median { get; set; }
            public float fivePercent { get; set; }
            public float max { get; set; }
            public float min { get; set; }
            public bool highToLow { get; set; }
            public long generated { get; set; }
        }

        public class Forquery1
        {
            public object bid { get; set; }
            public int[] types { get; set; }
            public object[] regions { get; set; }
            public object[] systems { get; set; }
            public int hours { get; set; }
            public int minq { get; set; }
        }

        public class Sell
        {
            public Forquery2 forQuery { get; set; }
            public Int64 volume { get; set; }
            public float wavg { get; set; }
            public float avg { get; set; }
            public float variance { get; set; }
            public float stdDev { get; set; }
            public float median { get; set; }
            public float fivePercent { get; set; }
            public float max { get; set; }
            public float min { get; set; }
            public bool highToLow { get; set; }
            public long generated { get; set; }
        }

        public class Forquery2
        {
            public bool bid { get; set; }
            public int[] types { get; set; }
            public object[] regions { get; set; }
            public object[] systems { get; set; }
            public int hours { get; set; }
            public int minq { get; set; }
        }

        public class SystemList
        {
            public int[] system { get; set; }
        }


        public class SystemData
        {
            public int star_id { get; set; }
            public int system_id { get; set; }
            public string name { get; set; }
            public JsonClasses.Position position { get; set; }
            public float security_status { get; set; }
            public int constellation_id { get; set; }
            public JsonClasses.Planet[] planets { get; set; }
            public string security_class { get; set; }
            public int[] stargates { get; set; }
            public int[] stations { get; set; }
        }

        public class ShipType
        {
            public int type_id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public bool published { get; set; }
            public int group_id { get; set; }
            public float radius { get; set; }
            public float volume { get; set; }
            public float capacity { get; set; }
            public int portion_size { get; set; }
            public float mass { get; set; }
            public int graphic_id { get; set; }
            public JsonClasses.Dogma_Attributes[] dogma_attributes { get; set; }
            public JsonClasses.Dogma_Effects[] dogma_effects { get; set; }
        }

        public class Buy
        {
            public Forquery forQuery { get; set; }
            public Int64 volume { get; set; }
            public float wavg { get; set; }
            public float avg { get; set; }
            public float variance { get; set; }
            public float stdDev { get; set; }
            public float median { get; set; }
            public float fivePercent { get; set; }
            public float max { get; set; }
            public float min { get; set; }
            public bool highToLow { get; set; }
            public long generated { get; set; }
        }
    }
}