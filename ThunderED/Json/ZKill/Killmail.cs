using System;

namespace ThunderED.Json.ZKill
{
    public partial class JsonZKill
    {
        public class Killmail
        {
            public long killmail_id { get; set; }
            public DateTime killmail_time { get; set; }
            public Victim victim { get; set; }
            public Attacker[] attackers { get; set; }
            public long solar_system_id { get; set; }
            public Zkb zkb;
        }
    }
}