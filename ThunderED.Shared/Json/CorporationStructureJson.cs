using System;

namespace ThunderED.Json
{
    public class CorporationStructureJson
    {
        public long structure_id;
        public long type_id;
        public long system_id;
        public long corporation_id;
        public DateTime? fuel_expires;
        public DateTime? next_reinforce_apply;
        public int reinforce_hour;
        public int reinforce_weekday;
        public int next_reinforce_hour;
        public int next_reinforce_weekday;
        public long profile_id;
        public CorpStructureStateEnum state;
        public DateTime? state_timer_end;
        public DateTime? state_timer_start;
        public DateTime? unanchors_at;
    }

    public enum CorpStructureStateEnum
    {
        anchor_vulnerable, anchoring, armor_reinforce, armor_vulnerable, deploy_vulnerable, fitting_invulnerable, hull_reinforce, hull_vulnerable, online_deprecated, onlining_vulnerable, shield_vulnerable, unanchored, unknown
    }
}
