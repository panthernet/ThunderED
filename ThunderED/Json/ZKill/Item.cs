namespace ThunderED.Json.ZKill
{
    internal partial class JsonZKill
    {
        public class Item
        {
            public int item_type_id { get; set; }
            public int singleton { get; set; }
            public int flag { get; set; }
            public int quantity_dropped { get; set; }
            public int quantity_destroyed { get; set; }
        }
    }
}