using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace ThunderED.Thd
{
    public class ThdMoonTableEntry
    {
        public long Id { get; set; }
        public long OreId { get; set; }
        public long SystemId { get; set; }
        public long PlanetId { get; set; }
        public long MoonId { get; set; }
        public double OreQuantity { get; set; }
        public long RegionId { get; set; }
        public string OreName { get; set; }
        public string MoonName { get; set; }
        public string SystemName { get; set; }
        public string RegionName { get; set; }
        public string PlanetName { get; set; }
        public string Notes { get; set; }


    }
}
