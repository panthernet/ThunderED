namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class Type_id
        {
            public long type_id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public bool published { get; set; }
            public long group_id { get; set; }
            public float radius { get; set; }
            public float volume { get; set; }
            public float capacity { get; set; }
            public int portion_size { get; set; }
            public float mass { get; set; }
            public int graphic_id { get; set; }
            public Dogma_Attributes[] dogma_attributes { get; set; }
            public Dogma_Effects[] dogma_effects { get; set; }
        }
    }
}