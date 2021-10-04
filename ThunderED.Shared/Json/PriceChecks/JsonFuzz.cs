namespace ThunderED.Json.PriceChecks
{
    public class JsonFuzz
    {
        //FUZZ

        public class FuzzItems
        {
            public FuzzBuy buy { get; set; }
            public FuzzBuy sell { get; set; }
        }

        public class FuzzBuy
        {
            public double weightedAverage;
            public double max;
            public double min;
            public double stddev;
            public double median;
            public double volume;
            public int orderCount;
            public double percentile;
        }

        public class FuzzPrice
        {
            public long Id { get; set; }
            public double Sell { get; set; }
            public double Buy { get; set; }
        }
    }
}