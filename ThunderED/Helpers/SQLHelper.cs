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

        #region Query

        internal static async Task<T> Query<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.Query<T>(table, field, whereField, whereData);
        }

        internal static async Task<T> Query<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.Query<T>(table, field, where);
        }

        internal static async Task<List<T>> QueryList<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.QueryList<T>(table, field, whereField, whereData);
        }

        internal static async Task<List<T>> QueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.QueryList<T>(table, field, where);
        }

        #endregion
        
        #region Update

        internal static async Task Update(string table, string setField, object setData, string whereField, object whereData)
        {
            await Provider?.Update(table, setField, setData, whereField, whereData);
        }

        internal static async Task Update(string table, string setField, object setData, Dictionary<string, object> where)
        {
            await Provider?.Update(table, setField, setData, where);

        }

        internal static async Task InsertOrUpdate(string table, Dictionary<string, object> values)
        {
            await Provider?.InsertOrUpdate(table, values);
        }

        internal static async Task Insert(string table, Dictionary<string, object> values)
        {
            await Provider?.Insert(table, values);
        }
        #endregion

        #region Delete
        internal static async Task Delete(string table, string whereField = null, object whereValue = null)
        {
            await Provider?.Delete(table, whereField, whereValue);
        }

        internal static async Task Delete(string table, Dictionary<string, object> where)
        {
            await Provider?.Delete(table, where);
        }
        #endregion

        internal static async Task InsertOrUpdateTokens(string notifyToken, string userId, string mailToken, string contractsToken)
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

        internal static async Task<List<AuthUserEntity>> GetAuthUsersEx()
        {
            var res = await SelectData("authUsers", new[] {"*"});

            return res?.Select(item => new AuthUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                EveName = Convert.ToString(item[1]),
                CharacterId = Convert.ToInt64(item[2]),
                DiscordId = Convert.ToUInt64(item[3]),
                Group = Convert.ToString(item[4]),
                IsActive = (string) item[5] == "yes"
            }).ToList();
        }

        internal static async Task<List<AuthUserEntity>> GetAuthUsers(Dictionary<string,object> where = null)
        {
            var res = await SelectData("authUsers", new[] {"*"}, where);

            return res?.Select(ParseAuthUser).ToList();
        }

        internal static async Task<List<AuthUserEntity>> GetAuthUsersWithPerms(Dictionary<string,object> where = null)
        {
            var res = await SelectData("authUsers", new[] {"*"}, where);

            return res?.Select(ParseAuthUser).Where(a=> !string.IsNullOrEmpty(a.Data.Permissions)).ToList();
        }

        internal static async Task<List<AuthUserEntity>> GetAuthUsersWithPerms(int state)
        {
            var res = await SelectData("authUsers", new[] {"*"}, new Dictionary<string, object>{{"authState", state}});

            return res?.Select(ParseAuthUser).Where(a=> !string.IsNullOrEmpty(a.Data.Permissions)).ToList();
        }

        private static AuthUserEntity ParseAuthUser(object[] item)
        {
            return new AuthUserEntity
            {
                Id = Convert.ToInt64(item[0]),
                CharacterId = Convert.ToInt64(item[1]),
                DiscordId = Convert.ToUInt64(item[2]),
                GroupName = (string) item[3],
                RefreshToken = (string) item[4],
                AuthState = Convert.ToInt32(item[5]),
                Data = JsonConvert.DeserializeObject<AuthUserData>((string) item[6])
            };
        }

        public static async Task SaveAuthUser(AuthUserEntity user, bool insertOnly = false)
        {
            var dic = new Dictionary<string, object>();
            if(user.Id > 0)
                dic.Add("Id", user.Id);
            dic.Add("characterID", user.CharacterId);
            dic.Add("discordID", user.DiscordId);
            dic.Add("groupName", user.GroupName);
            dic.Add("refreshToken", user.RefreshToken);
            dic.Add("authState", user.AuthState);
            dic.Add("data", JsonConvert.SerializeObject(user.Data));
            if (insertOnly)
                await Insert("authUsers", dic);
            else await InsertOrUpdate("authUsers", dic);
        }

        internal static async Task<AuthUserEntity> GetAuthUserByDiscordId(ulong discordId, bool order = false)
        {
            var res = await SelectData("authUsers", new[] {"*"}, new Dictionary<string, object> {{"discordID", discordId}});

            return res?.Select(ParseAuthUser).FirstOrDefault();
        }

        internal static async Task<AuthUserEntity> GetAuthUserByCharacterId(long id, bool order = false)
        {
            var res = await SelectData("authUsers", new[] {"*"}, new Dictionary<string, object> {{"characterID", id}});

            return res?.Select(ParseAuthUser).FirstOrDefault();
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

        internal static async Task<T> SelectCache<T>(object whereValue, int maxDays)
            where T: class
        {
            return await Provider?.SelectCache<T>(whereValue, maxDays);
        }

        internal static async Task UpdateCache<T>(T data, object id, int days = 1) 
            where T : class
        {
            await Provider?.UpdateCache(data, id, days);
        }

        internal static async Task PurgeCache()
        {
            await Provider?.PurgeCache();
        }

        public static string LoadProvider()
        {
            var prov = SettingsManager.Settings.Database.DatabaseProvider;
            switch (prov)
            {
                case "sqlite":
                    Provider = new SqliteDatabaseProvider();
                    break;
                case "mysql":
                    Provider = new MysqlDatabaseProvider();
                    break;
                default:
                    LogHelper.LogInfo("Using default sqlite provider!").GetAwaiter().GetResult();
                    Provider = new SqliteDatabaseProvider();
                    break;
                //  return $"[CRITICAL] Unknown database provider {prov}!";

            }

            if (!EnsureDBExists())
            {
                return "[CRITICAL] Failed to check DB integrity or create new instance!";
            }

            //upgrade database
            if (!Upgrade().GetAwaiter().GetResult())
            {
                return "[CRITICAL] Failed to upgrade DB to latest version!";
            }

            return null;
        }

        public static async Task<List<TimerItem>> SelectTimers()
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

        public static async Task DeleteWhereIn(string table, string field, List<long> list, bool not)
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
            return !string.IsNullOrEmpty(await Query<string>("pendingUsers", "characterID", "characterID", characterId.ToString()));
        }

        public static async Task<bool> PendingUsersIsEntryActive(string code)
        {
            return !string.IsNullOrEmpty(await Query<string>("pendingUsers", "characterID", "authString", code)) && 
                 await Query<string>("pendingUsers", "active", "authString", code) == "1";
        }

        public static async Task<string> PendingUsersGetCode(long characterId)
        {
            return await Query<string>("pendingUsers", "authString", "characterID", characterId.ToString());
        }

        public static async Task PendingUsersSetCode(string code, ulong discordId)
        {
            await Update("pendingUsers", "discordID", discordId, "authString", code);
        }

        public static async Task<ulong> PendingUsersGetDiscordId(string code)
        {
            return await Query<ulong>("pendingUsers", "discordID", "authString", code);
        }

        
        public static async Task<long> PendingUsersGetCharacterId(string code)
        {
            return await Query<long>("pendingUsers", "characterID", "authString", code);

        }
        
        #endregion

        #region userTokens table

        public static async Task<ulong> GetAuthUserDiscordId(long characterId)
        {
            return await SQLHelper.Query<ulong>("authUsers", "discordID", "characterID", characterId);
        }

        public static async Task<AuthUserEntity> GetAuthUserByCode(string code)
        {
            var characterId = await Query<string>("pendingUsers", "characterID", "authString", code);
            return await GetAuthUserByCharacterId(Convert.ToInt64(characterId));
        }

        public static async Task SetAuthUserState(string code, int value)
        {
            var characterId = await Query<string>("pendingUsers", "characterID", "authString", code);
            await Update("authUsers", "authState", value, "characterID", Convert.ToInt64(characterId));
        }
        
        public static async Task<List<UserTokenEntity>> UserTokensGetAllEntriesEx(Dictionary<string, object> where = null)
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

        public static async Task<string> GetRefreshTokenForContracts(long charId)
        {
            return await SQLHelper.Query<string>("refreshTokens", "ctoken", "id", charId);
        }

        public static async Task DeleteAuthUsers(ulong discordId)
        {
            await Delete("authUsers", "discordID", discordId);
        }

        public static async Task InvalidatePendingUser(string remainder)
        {
            await Update("pendingUsers", "active", "0", "authString", remainder);
        }

        public static async Task DeleteAuthDataByCharId(long characterID)
        {
            await Delete("pendingUsers", "characterID", characterID.ToString());
            await Delete("authUsers", "characterID", characterID);
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

            await InsertOrUpdate("contracts", new Dictionary<string, object>
            {
                {"characterID", characterID},
                {isCorp ? "corpdata" : "data", string.IsNullOrEmpty(result) ? "[]" : result},
                {isCorp ? "data" : "corpdata", string.IsNullOrEmpty(d) ? "[]" : d}
            });
        }

        public static async Task SaveAuthStands(AuthStandsEntity data)
        {
            await InsertOrUpdate("standAuth", new Dictionary<string, object>
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
            await Delete("standAuth", "characterID", id);
        }

        private static bool EnsureDBExists()
        {
            return Provider?.EnsureDBExists().GetAwaiter().GetResult() ?? false;
        }
    }
}
