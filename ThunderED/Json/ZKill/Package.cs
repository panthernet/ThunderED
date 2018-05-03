namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Package
        {
            public int killID { get; set; }
            public Killmail killmail { get; set; }
            public Zkb zkb { get; set; }
        }
    }
}