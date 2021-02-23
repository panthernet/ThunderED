namespace ThunderED
{
    public class WebMiningLedgerEntry
    {
        public string CharacterName { get; set; }
        public string CorporationTicker { get; set; }
        public string OreName { get; set; }
        public long OreId { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }

        public string NameFilter => $"{CharacterName}|{CorporationTicker}";
    }
}
