using System.Collections.Generic;
using System.Threading.Tasks;
using ThunderED.Json.Internal;

namespace ThunderED.Classes
{
    /// <summary>
    /// Interface for database providers
    /// </summary>
    public interface IDatabasePovider
    {
        Task<T> SQLiteDataQuery<T>(string table, string field, Dictionary<string, object> where);
        Task<T> SQLiteDataQuery<T>(string table, string field, string whereField, object whereData);
        Task<List<T>> SQLiteDataQueryList<T>(string table, string field, string whereField, object whereData);
        Task<List<T>> SQLiteDataQueryList<T>(string table, string field, Dictionary<string, object> where);

        Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData);
        Task SQLiteDataUpdate(string table, string setField, object setData, Dictionary<string, object> where);

        Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null);
        Task SQLiteDataDelete(string table, Dictionary<string, object> where);
        Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken, string contractsToken);
        Task RunCommand(string query2, bool silent);
        Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays);
        Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1);
        Task SQLiteDataPurgeCache();
        Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values);
        Task<List<TimerItem>> SQLiteDataSelectTimers();
        Task CleanupNotificationsList();
        Task SQLiteDataDeleteWhereIn(string table, string field, List<long> list, bool not);
        Task<bool> RunScript(string file);
        Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where = null);
        Task<bool> IsEntryExists(string table, Dictionary<string, object> where);
        Task SQLiteDataInsert(string table, Dictionary<string, object> values);
    }
}