using ThunderED.Json;

namespace ThunderED.Thd
{
    public class ThdStarSystem
    {
        public long RegionId { get; set; }
        public long ConstellationId { get; set; }
        public long SolarSystemId { get; set; }
        public string SolarSystemName{ get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double? XMin { get; set; }
        public double? YMin { get; set; }
        public double? ZMin { get; set; }
        public double? XMax { get; set; }
        public double? YMax { get; set; }
        public double? ZMax { get; set; }
        public double? Luminosity { get; set; }
        public short? Border { get; set; }
        public short? Fringe { get; set; }
        public short? Corridor { get; set; }
        public short? Hub { get; set; }
        public short? International { get; set; }
        public short? Regional { get; set; }
        public short? Constellation { get; set; }
        public double Security { get; set; }
        public long? FactionId { get; set; }
        public double? Radius { get; set; }
        public long? SunTypeId { get; set; }
        public string? SecurityClass{ get; set; }

        public bool IsUnreachable()
        {
            return IsWormhole() || IsAbyss();
        }

        public bool IsWormhole()
        {
            return !string.IsNullOrEmpty(SolarSystemName) && (SolarSystemId >= 31000000 && SolarSystemId <= 32000000);
        }

        public bool IsThera()
        {
            return !string.IsNullOrEmpty(SolarSystemName) && SolarSystemId == 31000005;
        }

        public bool IsAbyss()
        {
            return !string.IsNullOrEmpty(SolarSystemName) && (SolarSystemId >= 32000000 && SolarSystemId <= 33000000);
        }

        public static ThdStarSystem FromJson(JsonClasses.SystemName input)
        {
            return new ThdStarSystem
            {
                SolarSystemName = input.name,
                Security = input.security_status,
                RegionId = input.DB_RegionId ?? 0,
                ConstellationId = input.constellation_id,
                X = input.position.x,
                Y = input.position.y,
                Z = input.position.z,
                SolarSystemId = input.system_id
            };
        }
    }
}
