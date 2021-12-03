using System.Collections.Generic;
using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdContract
    {
        public long CharacterId { get; set; }
        public List<JsonClasses.Contract> Data { get; set; }
        public List<JsonClasses.Contract> CorpData { get; set; }
    }
}
