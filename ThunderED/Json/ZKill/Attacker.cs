namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Attacker
        {
            public float security_status { get; set; }
            public bool final_blow { get; set; }
            public int damage_done { get; set; }
            public long character_id { get; set; }
            public long corporation_id { get; set; }
            public long alliance_id { get; set; }
            public long ship_type_id { get; set; }
            public long weapon_type_id { get; set; }
        }
    }
}