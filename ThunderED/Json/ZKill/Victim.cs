namespace ThunderED.Json.ZKill
{
    internal partial class JsonZKill
    {
        public class Victim
        {
            public int damage_taken { get; set; }
            public int ship_type_id { get; set; }
            public int character_id { get; set; }
            public int corporation_id { get; set; }
            public int alliance_id { get; set; }
            public int faction_id { get; set; }
            public Item[] items { get; set; }
            public JsonClasses.Position position { get; set; }
        }
    }
}