using System;

namespace ThunderED.Thd
{
    public class ThdCacheEntry
    {
        public string Type { get; set; } = "-";
        public string Id { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.Now;
        public DateTime LastUpdate { get; set; } = DateTime.Now;
        public string Content { get; set; }
        public int Days { get; set; } = 1;
    }
}
