using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdIndustryJob
    {
        public long CharacterId { get; set; }
        public List<JsonClasses.IndustryJob> PersonalJobs { get; set; }
        public List<JsonClasses.IndustryJob> CorporateJobs { get; set; }
    }
}
