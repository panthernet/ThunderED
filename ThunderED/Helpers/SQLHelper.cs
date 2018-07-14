using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
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
            return await Provider?.SQLiteDataQuery<T>(table, field, whereField, whereData);
        }

        internal static async Task<T> SQLiteDataQuery<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.SQLiteDataQuery<T>(table, field, where);
        }

        internal static async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, string whereField, object whereData)
        {
            return await Provider?.SQLiteDataQueryList<T>(table, field, whereField, whereData);
        }

        internal static async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.SQLiteDataQueryList<T>(table, field, where);
        }

        #endregion
        
        //SQLite Update
        #region SQLiteUpdate

        internal static async Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData)
        {
            await Provider?.SQLiteDataUpdate(table, setField, setData, whereField, whereData);
        }

        internal static async Task SQLiteDataUpdate(string table, string setField, object setData, Dictionary<string, object> where)
        {
            await Provider?.SQLiteDataUpdate(table, setField, setData, where);

        }

        internal static async Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values)
        {
            await Provider?.SQLiteDataInsertOrUpdate(table, values);
        }

        internal static async Task SQLiteDataInsert(string table, Dictionary<string, object> values)
        {
            await Provider?.SQLiteDataInsert(table, values);
        }
        #endregion

        //SQLite Delete
        #region SQLiteDelete
        internal static async Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null)
        {
            await Provider?.SQLiteDataDelete(table, whereField, whereValue);
        }

        internal static async Task SQLiteDataDelete(string table, Dictionary<string, object> where)
        {
            await Provider?.SQLiteDataDelete(table, where);
        }
        #endregion

        internal static async Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken)
        {
            await Provider?.SQLiteDataInsertOrUpdateTokens(notifyToken, userId, mailToken);
        }

        internal static async Task<IList<IDictionary<string, object>>> GetAuthUser(ulong uId, bool order = false)
        {
            return await Provider?.GetAuthUser(uId, order);
        }

        internal static async Task<List<IDictionary<string, object>>> GetPendingUser(string remainder)
        {
            return await Provider?.GetPendingUser(remainder);
        }

        internal static async Task RunCommand(string query2, bool silent = false)
        {
            await Provider?.RunCommand(query2, silent);
        }

        internal static async Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays)
            where T: class
        {
            return await Provider?.SQLiteDataSelectCache<T>(whereValue, maxDays);
        }

        internal static async Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1) 
            where T : class
        {
            await Provider?.SQLiteDataUpdateCache(data, id, days);
        }

        internal static async Task SQLiteDataPurgeCache()
        {
            await Provider?.SQLiteDataPurgeCache();
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
            return await Provider?.SQLiteDataSelectTimers();
        }

        public static async Task CleanupNotificationsList()
        {
            await Provider?.CleanupNotificationsList();
        }

        public static async Task SQLiteDataDeleteWhereIn(string table, string field, List<int> list, bool not)
        {
            await Provider?.SQLiteDataDeleteWhereIn(table, field, list, not);

        }

        private static async Task<bool> RunScript(string file)
        {
            return await Provider?.RunScript(file);
        }

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByConstellation(int constellationId)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"constellationID", constellationId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt32(item[0]),
                constellation_id = Convert.ToInt32(item[1]),
                DB_RegionId = Convert.ToInt32(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<List<JsonClasses.SystemName>> GetSystemsByRegion(int regionId)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"regionID", regionId}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt32(item[0]),
                constellation_id = Convert.ToInt32(item[1]),
                DB_RegionId = Convert.ToInt32(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).ToList();
        }

        public static async Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where)
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
                    i.LastAnnounce = Convert.ToInt32(item[1]);
                    return i;
                }).ToList();
        }

        public static async Task<JsonClasses.SystemName> GetSystemById(int id)
        {
            return (await SelectData("mapSolarSystems", new[] {"solarSystemID", "constellationID", "regionID", "solarSystemName", "security"}, new Dictionary<string, object>
            {
                {"solarSystemID", id}
            })).Select(item => new JsonClasses.SystemName
            {
                system_id = Convert.ToInt32(item[0]),
                constellation_id = Convert.ToInt32(item[1]),
                DB_RegionId = Convert.ToInt32(item[2]),
                name = Convert.ToString(item[3]),
                security_status = (float)Convert.ToDouble(item[4]),
            }).FirstOrDefault();
        }
    }
}
