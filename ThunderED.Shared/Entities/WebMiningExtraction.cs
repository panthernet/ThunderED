using System;

namespace ThunderED
{
    public class WebMiningExtraction
    {
        public DateTime ChunkArrivalTime { get; set; }
        public DateTime ExtractionStartTime { get; set; }
        public DateTime NaturalDecayTime { get; set; }
        public long StructureId { get; set; }
        public long TypeId { get; set; }

        public string StructureName { get; set; }
        public string CorporationName { get; set; }
        public string Remains { get; set; }

        public string OreComposition { get; set; } = "-";
        public string Operator { get; set; } = "-";
    }
}
