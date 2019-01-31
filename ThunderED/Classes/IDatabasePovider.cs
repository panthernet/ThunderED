using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using ThunderED.Json.Internal;

namespace ThunderED.Classes
{
    /// <summary>
    /// Interface for database providers
    /// </summary>
    public interface IDatabasePovider
    {
        Task<T> Query<T>(string table, string field, Dictionary<string, object> where);
        Task Update(string table, string setField, object setData, Dictionary<string, object> where);
        Task<bool> Delete(string table, Dictionary<string, object> where);
        Task DeleteWhereIn(string table, string field, List<long> list, bool not);
        Task InsertOrUpdate(string table, Dictionary<string, object> values);
        Task<List<object[]>> SelectData(string query);
        Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where = null);
        Task Insert(string table, Dictionary<string, object> values);
        Task<bool> IsEntryExists(string table, Dictionary<string, object> where);

        Task RunCommand(string query2, bool silent);
        Task<bool> RunScript(string file);
        Task<bool> RunScriptText(string text);
        Task RunSystemCommand(string query2, bool silent = false);
        Task<bool> EnsureDBExists();

        Task<T> SelectCache<T>(object whereValue, int maxDays);
        Task UpdateCache<T>(T data, object id, int days = 1);
        Task PurgeCache();
        
        Task CleanupNotificationsList();
    }
}