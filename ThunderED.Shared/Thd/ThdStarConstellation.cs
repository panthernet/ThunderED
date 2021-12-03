using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdStarConstellation
    {
        public long RegionId { get; set; }
        public long ConstellationId { get; set; }
        public string ConstellationName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double ZMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public double ZMax { get; set; }
        public long FactionId { get; set; }
        public double Radius { get; set; }

        public static ThdStarConstellation FromJson(JsonClasses.ConstellationData input)
        {
            return new ThdStarConstellation
            {
                ConstellationName = input.name,
                RegionId = input.region_id,
                ConstellationId = input.constellation_id
            };
        }
    }
}