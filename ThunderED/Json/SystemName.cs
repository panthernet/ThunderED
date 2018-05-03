namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class SystemName
        {
            public int star_id { get; set; }
            public int system_id { get; set; }
            public string name { get; set; }
            public Position position { get; set; }
            public float security_status { get; set; }
            public int constellation_id { get; set; }
            public Planet[] planets { get; set; }
            public int[] stargates { get; set; }
            public int[] stations { get; set; }
        }
    }
}