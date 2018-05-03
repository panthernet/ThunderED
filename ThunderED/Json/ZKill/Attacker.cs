namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Attacker
        {
            public float security_status { get; set; }
            public bool final_blow { get; set; }
            public int damage_done { get; set; }
            public int character_id { get; set; }
            public int corporation_id { get; set; }
            public int alliance_id { get; set; }
            public int ship_type_id { get; set; }
            public int weapon_type_id { get; set; }
        }
    }
}