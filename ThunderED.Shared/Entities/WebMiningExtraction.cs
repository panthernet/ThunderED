using System;

namespace ThunderED
{
    public class WebMiningExtraction
    {
        public DateTime ChunkArrivalTime { get; set; }
        public DateTime ExtractionStartTime { get; set; }
        public long MoonId { get; set; }
        public DateTime NaturalDecayTime { get; set; }
        public long StructureId { get; set; }

        public string MoonName { get; set; }
        public string StructureName { get; set; }
        public string CorporationName { get; set; }
    }
}
