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

        Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData);
        Task SQLiteDataUpdate(string table, string setField, object setData, Dictionary<string, object> where);

        Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null);
        Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken);
        Task<IList<IDictionary<string, object>>> GetAuthUser(ulong uId, bool order = false);
        Task<List<IDictionary<string, object>>> GetPendingUser(string remainder);
        Task RunCommand(string query2, bool silent);
        Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays);
        Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1);
        Task SQLiteDataPurgeCache();
        Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values);
        Task<List<TimerItem>> SQLiteDataSelectTimers();
    }
}