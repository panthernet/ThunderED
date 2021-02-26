using System;

namespace ThunderED.Thd
{
    public class ThdNotificationListEntry
    {
        public string GroupName { get; set; }
        public string FilterName { get; set; } = "-";
        public long Id { get; set; }
        public DateTime Time { get; set; }
    }
}
