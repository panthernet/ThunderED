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

        public class UniverseIdTypes
        {
            public SimpleInventoryType[] inventory_types;
        }
        public class UniverseIdEntities
        {
            public SimpleInventoryType[] characters;
            public SimpleInventoryType[] alliances;
            public SimpleInventoryType[] corporations;
            public SimpleInventoryType[] factions;
        }

        public class UniverseIdMap
        {
            public SimpleInventoryType[] constellations;
            public SimpleInventoryType[] regions;
            public SimpleInventoryType[] systems;
        }

        public class UniverseIdStations
        {
            public SimpleInventoryType[] stations;
        }

        public class SimpleInventoryType
        {
            public long id;
            public string name;
        }
    }
}