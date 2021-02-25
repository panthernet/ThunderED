using System;
using System.Collections.Generic;
using System.Text;

namespace ThunderED.Thd
{
    public class ThdStructureInfo
    {
        public long StructureId { get; set; }
        public long StructureTypeId { get; set; }
        public string StructureName { get; set; }
        public string FuelTimeLeft { get; set; }
        public string State { get; set; }
        public long CorporationId { get; set; }
        public string CorporationName { get; set; }
        public long FeederId { get; set; }
        public DateTime? FuelTime { get; set; }
    }
}
