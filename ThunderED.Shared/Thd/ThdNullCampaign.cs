using System;
using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdNullCampaign
    {
        public string GroupKey { get; set; }
        public long CampaignId { get; set; }
        public DateTimeOffset Time { get; set; }
        public JsonClasses.NullCampaignItem Data { get; set; }
        public long LastAnnounce { get; set; }
    }
}
