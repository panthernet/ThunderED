using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Helpers;

namespace ThunderED.Json
{
    public partial class JsonClasses
    {
        //ESI Classes
        public class CharacterID
        {
            public long[] character { get; set; }
        }

        public class SearchInventoryType
        {
            public long[] inventory_type { get; set; }
        }


        public class CorpIDLookup
        {
            public long[] corporation { get; set; }
        }

        public class IDLookUp
        {
            public long[] idList;
        }


        public class SystemIDSearch
        {
            public long[] solar_system { get; set; }
        }

        public class SearchName
        {
            public long id { get; set; }
            public string name { get; set; }
            public string category { get; set; }
        }


        public class Position
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Planet
        {
            public string name;
            public long planet_id { get; set; }
            public long[] moons { get; set; }
        }

        public class Dogma_Attributes
        {
            public long attribute_id { get; set; }
            public float value { get; set; }
        }

        public class Dogma_Effects
        {
            public long effect_id { get; set; }
            public bool is_default { get; set; }
        }

        internal class NotificationSearch
        {
            public Notification[] list;
        }

        internal class Notification
        {
            public long notification_id;
            public string type;
            public long sender_id;
            public string sender_type;
            public string timestamp;
            public bool is_read;
            public string text;
        }

        internal class StructureData
        {
            public string name;
            public long solar_system_id;
        }

        
        internal class StationData
        {
            public string name;
            public long system_id;
            public long station_id;
        }

        internal class ConstellationData
        {
            public long constellation_id;
            public string name;
            public long region_id;
        }

        internal class RegionData
        {
            public string name;
            public long DB_id;
        }

        public class MailRecipient
        {
            public long recipient_id;
            public string recipient_type;
        }

        public class MailHeader
        {
            public long from;
            public bool is_read;
            public long[] labels;
            public long mail_id;
            public MailRecipient[] recipients;
            public string subject;
            public string timestamp;

            [JsonIgnore]
            public DateTime Date => DateTime.Parse(timestamp);

            [JsonIgnore]
            public long To { get; set; }
            [JsonIgnore]
            public string ToName { get; set; }
            [JsonIgnore]
            public string FromName { get; set; }
        }

        public class Mail
        {
            public string body;
            public long from;
            public long[] labels;
            public bool read;
            public string subject;
            public string timestamp;
        }

        public class MailLabelData
        {
            public MailLabel[] labels;
            public int total_unread_count;
        }

        public class MailLabel
        {
            public string color;
            public long label_id;
            public string name;
            public int unread_count;
        }

        public class IncursionsData
        {
            public IncursionData[] list;
        }

        public class IncursionData
        {
            public long constellation_id;
            public long faction_id;
            public bool has_boss;
            public List<long> infested_solar_systems = new List<long>();
            public float influence;
            public long staging_solar_system_id;
            public string state;
            public string type;
        }

        public class ServerStatus
        {
            public int players;
            public string server_version;
            public string start_time;
        }

        public class NullCampaignItem
        {
            public float attackers_score;
            public long campaign_id;
            public long constellation_id;
            public long defender_id;
            public float defender_score;
            public string event_type;
            public long solar_system_id;
            public string start_time;
            public long structure_id;

            [JsonIgnore]
            public DateTimeOffset Time => DateTimeOffset.Parse(start_time);

            [JsonIgnore] public long LastAnnounce;
        }

        internal class FWSystemStat
        {
            public string contested;
            public long occupier_faction_id;
            public long owner_faction_id;
            public long solar_system_id;
            public int victory_points;
            public int victory_points_threshold;
        }

        public class FWStats
        {
            public long faction_id;
            public FWStatsKills kills;

            public int pilots;
            public int systems_controlled;
        }

        public class FWStatsKills
        {
            public int last_week;
            public int total;
            public int yesterday;
        }

        public class CorporationHistoryEntry
        {
            public long corporation_id;
            public long record_id;
            public string start_date;
            public bool is_deleted;

            [JsonIgnore] public int Days;

            [JsonIgnore] public bool IsNpcCorp;

            [JsonIgnore] public string CorpName;

            [JsonIgnore]
            public DateTime Date => DateTime.Parse(start_date);

            [JsonIgnore] public string CorpTicker;
        }

        public class WalletJournalEntry
        {
            public double amount;
            public double balance;
            public long context_id;
            public string context_id_type;
            public string date;
            public string description;
            public long first_party_id;
            public long id;
            public string ref_type;
            public long second_party_id;

            [JsonIgnore]
            public DateTime DateEntry => DateTime.Parse(date);
        }

        public class WalletTransactionEntry
        {
            public long client_id;
            public string date;
            public bool is_buy;
            public bool is_personal;
            public long journal_ref_id;
            public long location_id;
            public long quantity;
            public long transaction_id;
            public long type_id;
            public double unit_price;

            [JsonIgnore]
            public DateTime DateEntry => DateTime.Parse(date);
        }

        public class CharYearlyStatsEntry
        {
            public CharYearlyStatsCharacterEntry character;
            public CharYearlyStatsCombatEntry combat;
            public CharYearlyStatsInvEntry inventory;
            public CharYearlyStatsIskEntry isk;
            public CharYearlyStatsMarketEntry market;
            public CharYearlyStatsModuleEntry module;
            public CharYearlyStatsPveEntry pve;
            public CharYearlyStatsSocialEntry social;
            public CharYearlyStatsTravelEntry travel;

            public int year;
        }

        public class Contract
        {
            public long acceptor_id;
            public long assignee_id;
            public string availability;//"public", "personal"
            public double buyout;
            public double collateral;
            public long contract_id;
            public string date_accepted;
            public string date_completed;
            public string date_expired;
            public string date_issued;
            public int days_to_complete;
            public long end_location_id;
            public bool for_corporation;
            public long issuer_corporation_id;
            public long issuer_id;
            public double price;
            public double reward;
            public long start_location_id;
            public string status;//"outstanding","finished"
            public string title;//desc
            public string type;// "item_exchange",courier
            public double volume;

            [JsonIgnore]
            public DateTime? DateExpired => string.IsNullOrEmpty(date_expired) ? null : (DateTime?)DateTime.Parse(date_expired).ToUniversalTime();
            [JsonIgnore]
            public DateTime? DateIssued => string.IsNullOrEmpty(date_issued) ? null : (DateTime?)DateTime.Parse(date_issued).ToUniversalTime();
            [JsonIgnore]
            public DateTime? DateCompleted => string.IsNullOrEmpty(date_completed) ? null : (DateTime?)DateTime.Parse(date_completed).ToUniversalTime();
            [JsonIgnore]
            public DateTime? DateAccepted => string.IsNullOrEmpty(date_accepted) ? null : (DateTime?)DateTime.Parse(date_accepted).ToUniversalTime();
        }

        public class Contact
        {
            public long contact_id;
            public string contact_type;// "character",character, corporation, alliance, faction 
            public bool is_blocked;
            public bool is_watched;
            public double standing;
        }


        public class CharYearlyStatsTravelEntry
        {
            public int acceleration_gate_activations;
            public int align_to;
            public long distance_warped_high_sec;
            public long distance_warped_low_sec;
            public long distance_warped_null_sec;
            public long distance_warped_wormhole;
            public int docks_high_sec;
            public int docks_low_sec;
            public int docks_null_sec;
            public int jumps_stargate_high_sec;
            public int jumps_stargate_low_sec;
            public int jumps_stargate_null_sec;
            public int jumps_wormhole;
            public int warps_high_sec;
            public int warps_low_sec;
            public int warps_null_sec;
            public int warps_to_bookmark;
            public int warps_to_celestial;
            public int warps_to_fleet_member;
            public int warps_to_scan_result;
            public int warps_wormhole;
        }

        public class CharYearlyStatsSocialEntry
        {
            public int add_contact_bad;
            public int add_contact_good;
            public int add_contact_high;
            public int add_note;
            public int added_as_contact_bad;
            public int added_as_contact_good;
            public int added_as_contact_high;
            public int added_as_contact_neutral;
            public int added_as_contact_horrible;
            public int chat_messages_alliance;
            public int chat_messages_corporation;
            public int chat_messages_fleet;
            public int chat_messages_solarsystem;
            public int chat_messages_warfaction;
            public ulong chat_total_message_length;
            public int direct_trades;
            public int fleet_broadcasts;
            public int fleet_joins;
        }

        public class CharYearlyStatsPveEntry
        {
            public int dungeons_completed_agent;
            public int dungeons_completed_distribution;
            public int missions_succeeded;
        }

        public class CharYearlyStatsModuleEntry
        {
            public int activations_hybrid_weapon;
            public int activations_interdiction_sphere_launcher;
            public int activations_missile_launcher;
            public int activations_probe_launcher;
            public int activations_projectile_weapon;
            public int link_weapons;
            public int overload;
            public int repairs;
        }

        public class CharYearlyStatsMarketEntry
        {
            public int accept_contracts_item_exchange;
            public int buy_orders_placed;
            public int cancel_market_order;
            public int create_contracts_courier;
            public int create_contracts_item_exchange;
            public long isk_gained;
            public long isk_spent;
            public int search_contracts;
            public int modify_market_order;
            public int sell_orders_placed;
        }

        public class CharYearlyStatsIskEntry
        {
            public long @in;
            public long @out;
        }

        public class CharYearlyStatsInvEntry
        {
            public int trash_item_quantity;
        }

        public class CharYearlyStatsCombatEntry
        {
            public int criminal_flag_set;
            public int deaths_high_sec;
            public int deaths_low_sec;
            public int duel_requested;
            public int pvp_flag_set;
            public int kills_assists;
            public int kills_high_sec;
            public int kills_low_sec;
            public int kills_null_sec;
            public int probe_scans;
        }

        public class CharYearlyStatsCharacterEntry
        {
            public int days_of_activity;
            public long minutes;
            public int sessions_started;
        }

        public class SkillsData
        {
            public List<SkillEntry> skills;
            public int total_sp;

            public async Task PopulateNames()
            {
                foreach (var skill in skills)
                {
                    var t = await SQLHelper.GetTypeId(skill.skill_id);
                    if(t == null) continue;
                    skill.DB_Name = t.name;
                    skill.DB_Description = t.description;
                    skill.DB_Group = t.group_id;
                    var g = await SQLHelper.GetInvGroup(skill.DB_Group);
                    if(g == null) continue;
                    skill.DB_GroupName = g.groupName;
                }
            }
        }

        public class SkillEntry
        {
            public int active_skill_level;
            public long skill_id;
            public long skillpoints_in_skill;
            public int trained_skill_level;
            public string DB_Name;
            public string DB_Description;
            public long DB_Group;
            public string DB_GroupName;
        }

        internal class invGroup
        {
            public long groupId;
            public long categoryId;
            public string groupName;
        }

        public class MailList
        {
            public long mailing_list_id;
            public string name;
        }

        public class ContractItem
        {
            public bool is_included;
            public bool is_singleton;
            public long quantity;
            public long record_id;
            public long type_id;

        }

        public class CorpIconsData
        {
            public string px128x128;
            public string px256x256;
            public string px64x64;
        }

        public class StandingData
        {
            public long from_id;
            public string from_type; //agent, npc_corp, faction 
            public double standing;
        }

        public class CharacterLocation
        {
            public long solar_system_id;
            public long station_id;
            public long structure_id;
        }

        public class CharacterShip
        {
            public long ship_item_id;
            public string ship_name;
            public long ship_type_id;
        }

        public class SovStructureData
        {
            public long alliance_id;
            public long solar_system_id;
            public long structure_id;
            public long structure_type_id;
            public double vulnerability_occupancy_level;
            public DateTime vulnerable_end_time;
            public DateTime vulnerable_start_time;
        }

        public class SearchResult
        {
            public List<long> solar_system = new List<long>();
            public List<long> region = new List<long>();
            public List<long> constellation = new List<long>();
            public List<long> alliance = new List<long>();
            public List<long> character = new List<long>();
            public List<long> corporation = new List<long>();
            public List<long> faction = new List<long>();
            public List<long> station = new List<long>();
            public List<long> inventory_type = new List<long>();
        }

        public class MoonData
        {
            public long moon_id;
            public string name;
            public long system_id;
        }

        public class IndustryJob
        {
            public long activity_id;
            public long blueprint_id;
            public long blueprint_location_id;
            public long blueprint_type_id;
            public long completed_character_id;
            public DateTime? completed_date;
            public double cost;
            public int duration;
            public DateTime? end_date;
            public long facility_id;
            public long installer_id;
            public long job_id;
            public int licensed_runs;
            public long output_location_id;
            public DateTime? pause_date;
            public double probability;
            public long product_type_id;
            public int runs;
            public DateTime? start_date;
            public long station_id;
            public string status;
            public int successful_runs;

            [JsonIgnore]
            public IndustryJobActivity Activity => (IndustryJobActivity)activity_id;
            [JsonIgnore]
            public IndustryJobStatusEnum StatusValue
            {
                get
                {
                    switch (status.ToLower())
                    {
                        case "active":
                            return IndustryJobStatusEnum.active;
                        case "ready":
                            return IndustryJobStatusEnum.ready;
                        case "paused":
                            return IndustryJobStatusEnum.paused;
                        case "delivered":
                            return IndustryJobStatusEnum.delivered;
                        case "reverted":
                            return IndustryJobStatusEnum.reverted;
                        case "cancelled":
                            return IndustryJobStatusEnum.cancelled;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
    }

    public enum IndustryJobStatusEnum
    {
        active, cancelled, delivered, paused, ready, reverted 
    }

    public enum IndustryJobActivity
    {
        none = 0,
        build = 1,
        techResearch = 2,
        te = 3,
        me = 4,
        copy = 5,
        duplicating = 6,
        reverseEng = 7,
        inventing = 8,
        reaction2 = 9,
        reaction = 11
    }
}