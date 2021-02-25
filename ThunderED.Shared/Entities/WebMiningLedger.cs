using System;

namespace ThunderED
{
    public class WebMiningLedger
    {
        public string StructureName { get; set; }
        public DateTime Date { get; set; }
        public long CorporationId { get; set; }
        public string CorporationName { get; set; }
        public long StructureId { get; set; }
        public long FeederId { get; set; }
        public long TypeId { get; set; }
        public string Stats { get; set; }
    }
}
