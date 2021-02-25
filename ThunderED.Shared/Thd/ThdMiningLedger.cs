using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using ThunderED.Helpers;

namespace ThunderED.Thd
{
    public class ThdMiningLedger
    {
        public long Id { get; set; }
        public long CitadelId { get; set; }
        public DateTime? Date { get; set; }
        public string OreJson;


        public void Unpack()
        {
            RawOre = RawOre.FromJson(OreJson);
        }

        [NotMapped]
        public Dictionary<long, int> RawOre { get; set; }
    }
}
