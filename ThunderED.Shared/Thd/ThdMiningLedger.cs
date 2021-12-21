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
        public string Stats;
        public int RefineEff;
        public LedgerPaymentSettings PaymentSettings;
        public LedgerPaymentData PaymentData;
        public List<WebMiningLedgerEntry> LedgerData;
        public bool Closed;


        public void Unpack()
        {
            try
            {
                RawOre = RawOre.FromJson(OreJson);
            }
            catch
            {
                RawOre = new Dictionary<long, int>();
            }
        }

        [NotMapped]
        public Dictionary<long, int> RawOre { get; set; }
    }

    public class LedgerPaymentSettings
    {

    }

    public class LedgerPaymentData
    {

    }

}
