using System;
using System.Collections.Generic;
using System.Text;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        public class ESIKill
        {
            //public Victim victim { get; set; }
            public ESIAttacker[] attackers { get; set; }
            public int killmail_id { get; set; }
            public DateTime killmail_time { get; set; }
            public int solar_system_id { get; set; }
            public ESIVictim victim;
        }

        public class ESIAttacker
        {
            public long alliance_id;
            public long character_id;
            public long corporation_id;
            public long damage_done;
            public bool final_blow;
            public float security_status;
            public long ship_type_id;
            public long weapon_type_id;
        }

        public class ESIVictim
        {
            public long alliance_id;
            public long character_id;
            public long corporation_id;
            public long damage_taken;
            public ESIItem[] items;
            public long ship_type_id;
        }

        public class ESIItem
        {
            public int flag;
            public long item_type_id;
            public int quantity_dropped;
            public int singleton;
        }
    }
}
