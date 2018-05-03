namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Zkb
        {
            public int locationID { get; set; }
            public string hash { get; set; }
            public float fittedValue { get; set; }
            public float totalValue { get; set; }
            public int points { get; set; }
            public bool npc { get; set; }
            public bool solo { get; set; }
            public bool awox { get; set; }
            public string href { get; set; }
        }
    }
}