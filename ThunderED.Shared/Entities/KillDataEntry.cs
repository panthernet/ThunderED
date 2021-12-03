using System.Collections.Generic;
using ThunderED.Json;
using ThunderED.Json.ZKill;
using ThunderED.Thd;

namespace ThunderED
{
    public class KillDataEntry
    {
        public long killmailID;
        public string killTime;
        public long victimShipID;
        public long attackerShipID;
        public float value;
        public long victimCharacterID;
        public long victimCorpID;
        public long victimAllianceID;
        public JsonZKill.Attacker[] attackers;
        public JsonZKill.Attacker finalBlowAttacker;
        public long finalBlowAttackerCharacterId;
        public long finalBlowAttackerCorpId;
        public long finalBlowAttackerAllyId;
        public bool isNPCKill;
        public long systemId;
        public ThdStarSystem rSystem;
        public JsonClasses.CorporationData rVictimCorp;
        public JsonClasses.CorporationData rAttackerCorp;
        public JsonClasses.AllianceData rVictimAlliance;
        public JsonClasses.AllianceData rAttackerAlliance;
        public string sysName;
        public JsonClasses.Type_id rVictimShipType;
        public JsonClasses.Type_id rAttackerShipType;
        public JsonClasses.CharacterData rVictimCharacter;
        public JsonClasses.CharacterData rAttackerCharacter;
        public string systemSecurityStatus;
        public Dictionary<string, string> dic;
        public bool isUnreachableSystem;
    }
}
