using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThunderED
{
    public class WebMiningLedgerEntry
    {
        public string CharacterName { get; set; }
        public long CharacterId { get; set; }
        public string CorporationTicker { get; set; }
        public string OreName { get; set; }
        public long OreId { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }

        public List<WebMiningLedgerEntry> Alts { get; set; } = new List<WebMiningLedgerEntry>();
        public string AltData { get; set; }

        public void Recalculate()
        {
            Price = Price + Alts.Where(a => a.OreId == OreId).Sum(a => a.Price);
            Quantity = Quantity + Alts.Where(a => a.OreId == OreId).Sum(a => a.Quantity);

            if (Alts.Any())
            {
                var sb = new StringBuilder();
                foreach (var alt in Alts.OrderByDescending(a=> a.Quantity))
                {
                    sb.Append($"{alt.CharacterName} [{alt.CorporationTicker}] {alt.Quantity} / ({alt.Price} ISK)\n");
                }

                AltData = sb.ToString();
            }
            else AltData = null;
        }

        public string NameFilter => $"{CharacterName}|{CorporationTicker}";
    }
}
