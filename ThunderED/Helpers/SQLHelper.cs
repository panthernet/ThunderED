using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Providers;

namespace ThunderED.Helpers
{
    public static partial class SQLHelper
    {
        public static IDatabasePovider Provider { get; set; }

        //SQLite Query
        #region SQLiteQuery

        internal static async Task<T> SQLiteDataQuery<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.Query<T>(table, field, whereField, whereData);
        }

        internal static async Task<T> SQLiteDataQuery<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.Query<T>(table, field, where);
        }

        internal static async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.QueryList<T>(table, field, whereField, whereData);
        }

        internal static async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.QueryList<T>(table, field, where);
        }

        #endregion
        
        //SQLite Update
        #region SQLiteUpdate

        internal static async Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData)
        {
            await Provider?.Update(table, setField, setData, whereField, whereData);
        }

        internal static async Task SQLiteDataUpdate(string table, string setField, object setData, Dictionary<string, object> where)
        {
            await Provider?.Update(table, setField, setData, where);

        }

        internal static async Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values)
        {
            await Provider?.InsertOrUpdate(table, values);
        }

        internal static async Task SQLiteDataInsert(string table, Dictionary<string, object> values)
        {
            await Provider?.Insert(table, values);
        }
        #endregion

        //SQLite Delete
        #region SQLiteDelete
        internal static async Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null)
        {
            await Provider?.Delete(table, whereField, whereValue);
        }

        internal static async Task SQLiteDataDelete(string table, Dictionary<string, object> where)
        {
            await Provider?.Delete(table, where);
        }
        #endregion

        internal static async Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken, string contractsToken)
        {   
            if (string.IsNullOrEmpty(notifyToken) && string.IsNullOrEmpty(mailToken) || string.IsNullOrEmpty(userId))
            {
                if(string.IsNullOrEmpty(contractsToken))
                    return;
            }

            var mail = string.IsNullOrEmpty(mailToken) ? await Provider.Query<string>("refreshTokens", "mail", "id", userId) : mailToken;
            var token = string.IsNullOrEmpty(notifyToken) ? await Provider.Query<string>("refreshTokens", "token", "id", userId) : notifyToken;
            var ctoken = string.IsNullOrEmpty(contractsToken) ? await Provider.Query<string>("refreshTokens", "ctoken", "id", userId) : contractsToken;
            token = token ?? "";
            mail = mail ?? "";
            ctoken = ctoken ?? "";

            await Provider.InsertOrUpdate("refreshTokens", new Dictionary<string, object>
            {
                {"id", userId},
                {"token", token},
                {"mail", mail},
                {"ctoken", ctoken}
            });
        }

        internal static async Task<AuthUserEntity> GetAuthUser(ulong discordId, bool order = false)
        {
            var res = await SelectData("authUsers", new[] {"*"}, new Dictionary<string, object> {{"discordID", discordId}});

            return res?.Select(item => new AuthUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                EveName = Convert.ToString(item[1]),
                CharacterId = Convert.ToInt64(item[2]),
                DiscordId = Convert.ToUInt64(item[3]),
                Group = Convert.ToString(item[4]),
                IsActive = (string) item[5] == "yes"
            }).FirstOrDefault();
        }

        
       

        internal static async Task<PendingUserEntity> GetPendingUser(string remainder)
        {
            var res = await SelectData("pendingUsers", new[] {"*"}, new Dictionary<string, object> {{"authString", remainder}});
            return res.Select(item => new PendingUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                CharacterId = Convert.ToInt64(item[1]),
                CorporationId = Convert.ToInt64(item[2]),
                AllianceId = Convert.ToInt64(item[3]),
                Groups = Convert.ToString(item[4]),
                AuthString = Convert.ToString(item[5]),
                Active = (string)item[6] == "1",
                CreateDate = Convert.ToDateTime(item[7]),
                DiscordId = Convert.ToInt64(item[8]),
            }).ToList().FirstOrDefault();
        }

        internal static async Task RunCommand(string query2, bool silent = false)
        {
            await Provider?.RunCommand(query2, silent);
        }

        internal static async Task<List<PendingUserEntity>> GetPendingUsers()
        {
            return (await SelectData("pendingUsers", new[] {"*"})).Select(item => new PendingUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                CharacterId = Convert.ToInt64(item[1]),
                CorporationId = Convert.ToInt64(item[2]),
                AllianceId = Convert.ToInt64(item[3]),
                Groups = Convert.ToString(item[4]),
                AuthString = Convert.ToString(item[5]),
                Active = item[6] == "1",
                CreateDate = Convert.ToDateTime(item[7]),
                DiscordId = Convert.ToInt64(item[8]),
            }).ToList();
        }

        internal static async Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays)
            where T: class
        {
            return await Provider?.SelectCache<T>(whereValue, maxDays);
        }

        internal static async Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1) 
            where T : class
        {
            await Provider?.UpdateCache(data, id, days);
        }

        internal static async Task SQLiteDataPurgeCache()
        {
            await Provider?.PurgeCache();
        }

        public static string LoadProvider()
        {
            var prov = SettingsManager.Settings.Config.DatabaseProvider;
            switch (prov)
            {
                case "sqlite":
                    Provider = new SqliteDatabaseProvider();
                    break;
                default:
                    LogHelper.LogInfo("Using default sqlite provider!").GetAwaiter().GetResult();
                    Provider = new SqliteDatabaseProvider();
                    break;
                //  return $"[CRITICAL] Unknown database provider {prov}!";

            }
            //upgrade database
            if (!SQLHelper.Upgrade().GetAwaiter().GetResult())
            {
                return "[CRITICAL] Failed to upgrade DB to latest version!";
            }

            return null;
        }

        public static async Task<List<TimerItem>> SQLiteDataSelectTimers()
        {
            return (await SelectData("timers", new[] {"*"})).Select(item => new TimerItem
            {
                id = Convert.ToInt64(item[0]),
                timerType = Convert.ToInt32(item[1]),
                timerStage = Convert.ToInt32(item[2]),
                timerLocation = (string)item[3],
                timerOwner = (string)item[4],
                timerET = (string)item[5],
                timerNotes = (string)item[6],
                timerChar = (string)item[7],
                announce = Convert.ToInt32(item[8])
            }).ToList();
        }

        public static async Task CleanupNotificationsList()
        {
            await Provider?.CleanupNotificationsList();
        }

        public static async Task SQLiteDataDeleteWhereIn(string table, string field, List<long> list, bool not)
        {
            await Provider?.DeleteWhereIn(table, field, list, not);

        }

        private static async Task<bool> RunScript(string file)
        {
            return await Provider?.RunScript(file);
        }

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByConstellation(long constellationId)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"constellationID", constellationId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByRegion(long regionId)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"regionID", regionId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where = null)
        {
            return await Provider?.SelectData(table, fields, where);
        }

        public static async Task<bool> IsEntryExists(string table, Dictionary<string, object> where)
        {
            return await Provider?.IsEntryExists(table, where);
        }

        public static async Task<List<JsonClasses.NullCampaignItem>> GetNullCampaigns(string group)
        {
            return (await SelectData("nullCampaigns", new[] {"data", "lastAnnounce" }, new Dictionary<string, object>{{"groupKey", group}}))
                .Select(item =>
                {                    
                    var i = new JsonClasses.NullCampaignItem().FromJson((string) item[0]);
                    i.LastAnnounce = Convert.ToInt64(item[1]);
                    return i;
                }).ToList();
        }

        public static async Task<JsonClasses.SystemName> GetSystemById(long id)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"solarSystemID", id}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                DB_RegionId = Convert.ToInt64(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).FirstOrDefault();
        }

        internal static async Task<JsonClasses.RegionData> GetRegionById(long id)
        {
            return (await SelectData("mapRegions", new[] {"regionID", "regionName"}, new Dictionary<string, object>
            {
                {"regionID", id}
            })).Select(item => new JsonClasses.RegionData
            {
                DB_id = Convert.ToInt64(item[0]),
                name = Convert.ToString(item[1]),
            }).FirstOrDefault();
        }

        internal static async Task<JsonClasses.ConstellationData> GetConstellationById(long id)
        {
            return (await SelectData("mapConstellations", new[] {"regionID", "constellationID","constellationName"}, new Dictionary<string, object>
            {
                {"constellationID", id}
            })).Select(item => new JsonClasses.ConstellationData
            {
                region_id = Convert.ToInt64(item[0]),
                constellation_id = Convert.ToInt64(item[1]),
                name = Convert.ToString(item[2]),
            }).FirstOrDefault();
        }

        
        internal static async Task<JsonClasses.Type_id> GetTypeId(long id)
        {
            return (await SelectData("invTypes", new[] {"typeID", "groupID","typeName", "description", "mass", "volume"}, new Dictionary<string, object>
            {
                {"typeID", id}
            })).Select(item => new JsonClasses.Type_id
            {
                type_id = Convert.ToInt64(item[0]),
                group_id = Convert.ToInt64(item[1]),
                name = Convert.ToString(item[2]),
                description = Convert.ToString(item[3]),
                mass = (float)Convert.ToDouble(item[4]),
                volume = (float)Convert.ToDouble(item[5])
            }).FirstOrDefault();
        }

        
        internal static async Task<JsonClasses.invGroup> GetInvGroup(long id)
        {
            return (await SelectData("invGroups", new[] {"groupID", "categoryID","groupName"}, new Dictionary<string, object>
            {
                {"groupID", id}
            })).Select(item => new JsonClasses.invGroup
            {
                groupId = Convert.ToInt64(item[0]),
                categoryId = Convert.ToInt64(item[1]),
                groupName = Convert.ToString(item[2]),
            }).FirstOrDefault();
        }

        #region pendingUsers table
        public static async Task<bool> PendingUsersIsEntryActive(long characterId)
        {
            return !string.IsNullOrEmpty(await SQLiteDataQuery<string>("pendingUsers", "characterID", "characterID", characterId.ToString()));
        }

        public static async Task<bool> PendingUsersIsEntryActive(string code)
        {
            return !string.IsNullOrEmpty(await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code)) && 
                 await SQLiteDataQuery<string>("pendingUsers", "active", "authString", code) == "1";
        }

        public static async Task<string> PendingUsersGetCode(long characterId)
        {
            return await SQLiteDataQuery<string>("pendingUsers", "authString", "characterID", characterId.ToString());
        }

        public static async Task PendingUsersSetCode(string code, ulong discordId)
        {
            await SQLiteDataUpdate("pendingUsers", "discordID", discordId, "authString", code);
        }

        public static async Task<ulong> PendingUsersGetDiscordId(string code)
        {
            return await SQLiteDataQuery<ulong>("pendingUsers", "discordID", "authString", code);
        }

        
        public static async Task<long> PendingUsersGetCharacterId(string code)
        {
            return await SQLiteDataQuery<long>("pendingUsers", "characterID", "authString", code);

        }
        
        #endregion

        #region userTokens table

        public static async Task<string> UserTokensGetGroupName(long characterId)
        {
            return await SQLiteDataQuery<string>("userTokens", "groupName", "characterID", characterId);
        }
        public static async Task<string> UserTokensGetGroupName(string code)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await UserTokensGetGroupName(Convert.ToInt64(characterId));
        }

        public static async Task<string> UserTokensGetName(long characterId)
        {
            return await SQLiteDataQuery<string>("userTokens", "characterName", "characterID", characterId);
        }

        public static async Task<string> UserTokensGetName(string code)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await UserTokensGetName(Convert.ToInt64(characterId));
        }

        public static async Task<bool> UserTokensIsAuthed(long characterId)
        {
            return await SQLiteDataQuery<int>("userTokens", "authState", "characterID", characterId) == 2;
        }

        public static async Task<bool> UserTokensIsConfirmed(long characterId)
        {
            return await SQLiteDataQuery<int>("userTokens", "authState", "characterID", characterId) == 1;
        }

        public static async Task<bool> UserTokensIsConfirmed(string code)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await UserTokensIsConfirmed(Convert.ToInt64(characterId));
        }

        public static async Task<bool> UserTokensIsPending(long characterId)
        {
            return await SQLiteDataQuery<int>("userTokens", "authState", "characterID", characterId) == 0;
        }

        public static async Task<bool> UserTokensIsPending(string code )
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await UserTokensIsPending(Convert.ToInt64(characterId));
        }

        public static async Task<bool> UserTokensExists(long characterId)
        {
            return await SQLiteDataQuery<long>("userTokens", "characterID", "characterID", characterId) != 0;
        }


        public static async Task<bool> UserTokensExists(string code)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await UserTokensExists(Convert.ToInt64(characterId));

        }

        public static async Task UserTokensSetDiscordId(string code, ulong authorId)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);

            await SQLiteDataUpdate("userTokens", "discordUserId", authorId, "characterID", Convert.ToInt64(characterId));
        }

        public static async Task<List<object[]>> UserTokensGetConfirmedDataList()
        {
            return await SelectData("userTokens", new[] {"characterID", "characterName", "discordUserId", "groupName"}, new Dictionary<string, object>
            {
                {"authState", 1}
            });
        }

        public static async Task<string> GetRefreshTokenForContracts(long charId)
        {
            return await SQLHelper.SQLiteDataQuery<string>("refreshTokens", "ctoken", "id", charId);
        }

        public static async Task UserTokensSetAuthState(long characterId, int value)
        {
            await SQLiteDataUpdate("userTokens", "authState", value, "characterID", characterId);
        }

        public static async Task UserTokensSetAuthState(string code, int value)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            await SQLiteDataUpdate("userTokens", "authState", value, "characterID", Convert.ToInt64(characterId));
        }
        
        public static async Task<bool> UserTokensHasDiscordId(string code)
        {
            var characterId = await SQLiteDataQuery<string>("pendingUsers", "characterID", "authString", code);
            return await SQLiteDataQuery<ulong>("userTokens", "discordUserId", "characterID", characterId) != 0;
        }

        public static async Task<List<UserTokenEntity>> UserTokensGetAllEntries(Dictionary<string, object> where = null)
        {
            var data = await SelectData("userTokens", new[] {"characterID", "characterName", "discordUserId", "refreshToken", "groupName", "permissions", "authState"}, where);
            var list = new List<UserTokenEntity>();
            data.ForEach(d =>
            {
                list.Add(new UserTokenEntity
                {
                    CharacterId = Convert.ToInt64(d[0]),
                    CharacterName = d[1].ToString(),
                    DiscordUserId = Convert.ToUInt64(d[2]),
                    RefreshToken = d[3].ToString(),
                    GroupName = d[4].ToString(),
                    Permissions = d[5].ToString(),
                    AuthState = Convert.ToInt32(d[6]),
                });
            });
            return list;
        }
        #endregion

        public static async Task<UserTokenEntity> UserTokensGetEntry(long inspectCharId)
        {
            return (await UserTokensGetAllEntries(new Dictionary<string, object> {{"characterID", inspectCharId}})).FirstOrDefault();
        }

        public static async Task DeleteAuthUsers(string discordId)
        {
            await SQLiteDataDelete("authUsers", "discordID", discordId);
        }

        public static async Task InvalidatePendingUser(string remainder)
        {
            await SQLiteDataUpdate("pendingUsers", "active", "0", "authString", remainder);
        }

        public static async Task DeleteAuthDataByCharId(long characterID)
        {
            await SQLiteDataDelete("userTokens", "characterID", characterID);
            await SQLiteDataDelete("pendingUsers", "characterID", characterID.ToString());
            await SQLiteDataDelete("authUsers", "characterID", characterID.ToString());
        }

        public static async Task<List<JsonClasses.Contract>> LoadContracts(long characterID, bool isCorp)
        {
            var data = (string)(await SelectData("contracts", new [] {isCorp ? "corpdata" : "data"}, new Dictionary<string, object> {{"characterID", characterID}}))?.FirstOrDefault()?.FirstOrDefault();
            return string.IsNullOrEmpty(data) ? null : JsonConvert.DeserializeObject<List<JsonClasses.Contract>>(data).OrderByDescending(a=> a.contract_id).ToList();
        }

        public static async Task SaveContracts(long characterID, List<JsonClasses.Contract> data, bool isCorp)
        {
            var result = JsonConvert.SerializeObject(data);

            var d = (string)(await SelectData("contracts", new [] {isCorp ? "data" : "corpdata"}, new Dictionary<string, object> {{"characterID", characterID}}))?.FirstOrDefault()?.FirstOrDefault();

            await SQLiteDataInsertOrUpdate("contracts", new Dictionary<string, object>
            {
                {"characterID", characterID},
                {isCorp ? "corpdata" : "data", string.IsNullOrEmpty(result) ? "[]" : result},
                {isCorp ? "data" : "corpdata", string.IsNullOrEmpty(d) ? "[]" : d}
            });
        }

        public static async Task SaveAuthStands(AuthStandsEntity data)
        {
            await SQLiteDataInsertOrUpdate("standAuth", new Dictionary<string, object>
            {
                {"characterID", data.CharacterID},
                {"token", data.Token},
                {"personalStands", JsonConvert.SerializeObject(data.PersonalStands)},
                {"corpStands", JsonConvert.SerializeObject(data.CorpStands)},
                {"allianceStands", JsonConvert.SerializeObject(data.AllianceStands)},
            });
        }

        public static async Task<AuthStandsEntity> LoadAuthStands(long id)
        {
            var data = (await SelectData("standAuth", new [] {"token", "personalStands", "corpStands", "allianceStands"}, new Dictionary<string, object> {{"characterID", id}}))?.FirstOrDefault();
            if (data == null)
                return null;
            return new AuthStandsEntity
            {
                CharacterID = id,
                Token = (string)data[0],
                PersonalStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[1]),
                CorpStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[2]),
                AllianceStands = JsonConvert.DeserializeObject<List<JsonClasses.Contact>>((string)data[3]),
            };
        }

        public static async Task DeleteAuthStands(long id)
        {
            await SQLiteDataDelete("standAuth", "characterID", id);
        }

    }
}
