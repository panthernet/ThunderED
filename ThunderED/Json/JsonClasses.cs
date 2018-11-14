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
            public int[] character { get; set; }
        }

        public class SearchInventoryType
        {
            public int[] inventory_type { get; set; }
        }


        public class CorpIDLookup
        {
            public int[] corporation { get; set; }
        }

        public class IDLookUp
        {
            public int[] idList;
        }


        public class SystemIDSearch
        {
            public int[] solar_system { get; set; }
        }

        public class SearchName
        {
            public int id { get; set; }
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
            public int planet_id { get; set; }
            public int[] moons { get; set; }
        }

        public class Dogma_Attributes
        {
            public int attribute_id { get; set; }
            public float value { get; set; }
        }

        public class Dogma_Effects
        {
            public int effect_id { get; set; }
            public bool is_default { get; set; }
        }

        internal class NotificationSearch
        {
            public Notification[] list;
        }

        internal class Notification
        {
            public int notification_id;
            public string type;
            public int sender_id;
            public string sender_type;
            public string timestamp;
            public bool is_read;
            public string text;
        }

        internal class StructureData
        {
            public string name;
            public int solar_system_id;
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
            public int recipient_id;
            public string recipient_type;
        }

        public class MailHeader
        {
            public int from;
            public bool is_read;
            public int[] labels;
            public int mail_id;
            public MailRecipient[] recipients;
            public string subject;
            public string timestamp;

            [JsonIgnore]
            public DateTime Date => DateTime.Parse(timestamp);
        }

        public class Mail
        {
            public string body;
            public int from;
            public int[] labels;
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
            public int label_id;
            public string name;
            public int unread_count;
        }

        public class IncursionsData
        {
            public IncursionData[] list;
        }

        public class IncursionData
        {
            public int constellation_id;
            public int faction_id;
            public bool has_boss;
            public List<int> infested_solar_systems = new List<int>();
            public float influence;
            public int staging_solar_system_id;
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
            public int campaign_id;
            public int constellation_id;
            public int defender_id;
            public float defender_score;
            public string event_type;
            public int solar_system_id;
            public string start_time;
            public long structure_id;

            [JsonIgnore]
            public DateTimeOffset Time => DateTimeOffset.Parse(start_time);

            [JsonIgnore] public int LastAnnounce;
        }

        internal class FWSystemStat
        {
            public string contested;
            public int occupier_faction_id;
            public int owner_faction_id;
            public long solar_system_id;
            public int victory_points;
            public int victory_points_threshold;
        }

        public class FWStats
        {
            public int faction_id;
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
            public int corporation_id;
            public int record_id;
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
            public DateTime? DateExpired => string.IsNullOrEmpty(date_expired) ? null : (DateTime?)DateTime.Parse(date_expired);
            [JsonIgnore]
            public DateTime? DateIssued => string.IsNullOrEmpty(date_issued) ? null : (DateTime?)DateTime.Parse(date_issued);
            [JsonIgnore]
            public DateTime? DateCompleted => string.IsNullOrEmpty(date_completed) ? null : (DateTime?)DateTime.Parse(date_completed);
            [JsonIgnore]
            public DateTime? DateAccepted => string.IsNullOrEmpty(date_accepted) ? null : (DateTime?)DateTime.Parse(date_accepted);
        }

        public class Contact
        {
            public long contact_id;
            public string contact_type;// "character",
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
    }
}