using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdSovIndexTracker
    {
        public string GroupName { get; set; }
        public List<JsonClasses.SovStructureData> Data { get; set; }
    }
}
