using System;

namespace ThunderED.Json.ZKill
{
    internal partial class JsonZKill
    {
        public class Kill
        {
            public int killmail_id { get; set; }
            public DateTime killmail_time { get; set; }
            public Victim victim { get; set; }
            public Attacker[] attackers { get; set; }
            public int solar_system_id { get; set; }
            public Zkb zkb { get; set; }
        }
    }
}