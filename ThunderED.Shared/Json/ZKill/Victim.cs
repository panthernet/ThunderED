namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Victim
        {
            public int damage_taken { get; set; }
            public long ship_type_id { get; set; }
            public long character_id { get; set; }
            public long corporation_id { get; set; }
            public long alliance_id { get; set; }
            public long faction_id { get; set; } //?
            public Item[] items { get; set; }
            public JsonClasses.Position position { get; set; }
        }
    }
}