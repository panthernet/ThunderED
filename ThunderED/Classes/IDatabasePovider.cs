using System.Collections.Generic;
using System.Threading.Tasks;

namespace ThunderED.Classes
{
    public interface IDatabasePovider
    {
        Task<string> SQLiteDataQuery(string table, string field, string whereField, object whereData);
        Task SQLiteDataUpdate(string table, string setField, string setData, string whereField, object whereData);
        Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null);
        Task SQLiteDataInsertOrUpdateTokens(string token, string userId);
        Task InsertPendingUser(string characterID, string corporationid, string allianceid, string authString, string active, string dateCreated);
        Task<IList<IDictionary<string, object>>> GetAuthUser(ulong uId, bool order = false);
        Task<List<IDictionary<string, object>>> GetPendingUser(string remainder);
        Task RunCommand(string query2);
        Task SQLiteDataInsertOrUpdateLastNotification(string characterID, string notifID);
        Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays);
        Task SQLiteDataUpdateCacheField<T>(string setField, object setData, object whereId);
        Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1);
        Task SQLiteDataPurgeCache();
    }
}