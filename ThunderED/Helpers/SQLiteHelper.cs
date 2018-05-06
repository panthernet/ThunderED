using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Providers;

namespace ThunderED.Helpers
{
    public static partial class SQLiteHelper
    {
        public static IDatabasePovider Provider { get; set; }

        //SQLite Query
        #region SQLiteQuery

        internal static async Task<string> SQLiteDataQuery(string table, string field, string whereField, object whereData)
        {
            return await Provider?.SQLiteDataQuery(table, field, whereField, whereData);
        }

        internal static async Task<string> SQLiteDataQuery(string table, string field, Dictionary<string, object> where)
        {
            return await Provider?.SQLiteDataQuery(table, field, where);
        }

        #endregion
        
        //SQLite Update
        #region SQLiteUpdate

        internal static async Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData)
        {
            await Provider?.SQLiteDataUpdate(table, setField, setData, whereField, whereData);
        }

        internal static async Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values)
        {
            await Provider?.SQLiteDataInsertOrUpdate(table, values);
        }
        #endregion

        //SQLite Delete
        #region SQLiteDelete
        internal static async Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null)
        {
            await Provider?.SQLiteDataDelete(table, whereField, whereValue);
        }
        #endregion

        internal static async Task SQLiteDataInsertOrUpdateTokens(string token, string userId)
        {
            if(string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId)) return;
            await Provider?.SQLiteDataInsertOrUpdateTokens(token, userId);
        }

        internal static async Task InsertPendingUser(string characterID, string corporationid, string allianceid, string authString, string active, string dateCreated)
        {
            await Provider?.InsertPendingUser(characterID, corporationid, allianceid, authString, active, dateCreated);
        }

        internal static async Task<IList<IDictionary<string, object>>> GetAuthUser(ulong uId, bool order = false)
        {
            return await Provider?.GetAuthUser(uId, order);
        }

        internal static async Task<List<IDictionary<string, object>>> GetPendingUser(string remainder)
        {
            return await Provider?.GetPendingUser(remainder);
        }

        internal static async Task RunCommand(string query2)
        {
            await Provider?.RunCommand(query2);
        }

        internal static async Task SQLiteDataInsertOrUpdateLastNotification(string characterID, string notifID)
        {
            await Provider?.SQLiteDataInsertOrUpdateLastNotification(characterID, notifID);
        }

        internal static async Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays)
            where T: class
        {
            return await Provider?.SQLiteDataSelectCache<T>(whereValue, maxDays);
        }

        internal static async Task SQLiteDataUpdateCacheField<T>(string setField, object setData, object whereId)
        {
            await Provider?.SQLiteDataUpdateCacheField<T>(setField, setData, whereId);
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
            var prov = SettingsManager.Get("config", "databaseProvider");
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
            if (!SQLiteHelper.Upgrade().GetAwaiter().GetResult())
            {
                return "[CRITICAL] Failed to upgrade DB to latest version!";
            }

            return null;
        }

        public static async Task<List<TimerItem>> SQLiteDataSelectTimers()
        {
            return await Provider?.SQLiteDataSelectTimers();
        }
    }
}
