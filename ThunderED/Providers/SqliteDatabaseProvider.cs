using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.Internal;

namespace ThunderED.Providers
{
    internal class SqliteDatabaseProvider : IDatabasePovider
    {
         //SQLite Query
        #region SQLiteQuery


        public async Task<bool> IsEntryExists(string table, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            if (where != null)
            {
                var last = where.Keys.Last();
                foreach (var pair in where)
                {
                    whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
                }
            }

            var whereTrueText = string.IsNullOrEmpty(whereText) ? null : $"WHERE {whereText}";

            var query = $"SELECT * FROM {table} {whereTrueText}";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));
                }

                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        return r.HasRows;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataQuery]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.SQLite);
                    return false;
                }
            }
        }

        public async Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            if (where != null)
            {
                var last = where.Keys.Last();
                foreach (var pair in where)
                {
                    whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
                }
            }

            var field = string.Join(',', fields);
            var whereTrueText = string.IsNullOrEmpty(whereText) ? null : $"WHERE {whereText}";

            var query = $"SELECT {field} FROM {table} {whereTrueText}";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));
                }

                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        var list = new List<object[]>();
                        if (r.HasRows)
                        {
                            while (await r.ReadAsync())
                            {
                                var obj = new List<object>();
                                for (int i = 0; i < fields.Length; i++)
                                {
                                    obj.Add(r.IsDBNull(i) ? null : r.GetValue(i));
                                }
                                list.Add(obj.ToArray());
                            }
                        }
                        return list;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataQuery]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.SQLite);
                    return default;
                }
            }
        }
      
        public async Task<T> SQLiteDataQuery<T>(string table, string field, Dictionary<string, object> where)
        {
            if (where == null)
            {
                await LogHelper.LogError($"[SQLiteDataQuery]: {table}-{field} query has empty values!");
                return default;
            }

            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"SELECT {field} FROM {table} WHERE {whereText}";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in where)
                {
                    querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));
                }
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        if (r.HasRows)
                        {
                            await r.ReadAsync();
                            if (r.IsDBNull(0))
                                return default;
                            var type = typeof(T);
                            if(type == typeof(string))
                                return (T)(object)(r.IsDBNull(0) ? "" : r.GetString(0));
                            if (type == typeof(int))
                                return (T) (object) (r.IsDBNull(0) ? 0 : r.GetInt32(0));
                            if (type == typeof(ulong))
                                return (T) (object) (r.IsDBNull(0) ? 0 : (ulong)r.GetInt64(0));
                            if (type == typeof(long))
                                return (T) (object) (r.IsDBNull(0) ? 0 : r.GetInt64(0));
                        }
                        return default;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataQuery]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                    return default;
                }
            }
        }

        public async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            if (where == null)
            {
                await LogHelper.LogError($"[SQLiteDataQuery]: {table}-{field} query has empty values!");
                return default;
            }

            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"SELECT {field} FROM {table} WHERE {whereText}";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in where)
                {
                    querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));
                }
                var res = new List<T>();
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        if (!r.HasRows) return res;
                        while (await r.ReadAsync())
                        {
                            if (r.IsDBNull(0))
                                continue;
                            var type = typeof(T);
                            if (type == typeof(string))
                                res.Add((T) (object) (r.IsDBNull(0) ? "" : r.GetString(0)));
                            else if (type == typeof(int))
                                res.Add((T) (object) (r.IsDBNull(0) ? 0 : r.GetInt32(0)));
                            else if (type == typeof(ulong))
                                res.Add((T) (object) (r.IsDBNull(0) ? 0 : (ulong)r.GetInt64(0)));
                            else if (type == typeof(long))
                                res.Add((T) (object) (r.IsDBNull(0) ? 0 : r.GetInt64(0)));

                        }

                        return res;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataQuery]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                    return res;
                }
            }
        }

        public async Task<T> SQLiteDataQuery<T>(string table, string field, string whereField, object whereData)
        {
            return await SQLiteDataQuery<T>(table, field, new Dictionary<string, object> {{whereField, whereData}});
        }

        public async Task<List<T>> SQLiteDataQueryList<T>(string table, string field, string whereField, object whereData)
        {
            return await SQLiteDataQueryList<T>(table, field, new Dictionary<string, object> {{whereField, whereData}});
        }

        #endregion
        
        //SQLite Update
        #region SQLiteUpdate

        public async Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values)
        {
            if (values == null)
            {
                await LogHelper.LogError($"[SQLiteDataInsertOrUpdate]: {table} query has empty values!");
                return;
            }
            var fromText = string.Empty;
            var valuesText = string.Empty;
            int count = 1;
            var last = values.Keys.Last();
            foreach (var pair in values)
            {
                fromText += $"{pair.Key}{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            var query = $"insert or replace into {table} ({fromText}) values({valuesText})";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in values)
                {
                    querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    querySQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataInsertOrUpdate]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataInsert(string table, Dictionary<string, object> values)
        {
            if (values == null)
            {
                await LogHelper.LogError($"[SQLiteDataInsert]: {table} query has empty values!");
                return;
            }
            var fromText = string.Empty;
            var valuesText = string.Empty;
            int count = 1;
            var last = values.Keys.Last();
            foreach (var pair in values)
            {
                fromText += $"{pair.Key}{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            var query = $"insert into {table} ({fromText}) values({valuesText})";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in values)
                {
                    querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    querySQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataInsert]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData)
        {
            await SQLiteDataUpdate(table, setField, setData, new Dictionary<string, object> {{whereField, whereData}});
        }

        public async Task SQLiteDataUpdate(string table, string setField, object setData, Dictionary<string, object> where)
        {
            if (where == null)
            {
                await LogHelper.LogError($"[SQLiteDataUpdate]: {table} query has empty values!");
                return;
            }
            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"UPDATE {table} SET {setField} = @data WHERE {whereText}";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();
                count = 1;
                insertSQL.Parameters.Add(new SqliteParameter("@data", setData ?? DBNull.Value));
                foreach (var pair in where)
                    insertSQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value ?? DBNull.Value));
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataUpdate]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            }
        }
        #endregion

        //SQLite Delete
        #region SQLiteDelete
        public async Task SQLiteDataDelete(string table, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand($"DELETE FROM {table} WHERE {whereText}", con))
            {
                await con.OpenAsync();
                count = 1;
                foreach (var pair in where)
                    insertSQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));

                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataDelete", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataDelete(string table, string whereField = null, object whereValue = null)
        {
            var where = string.IsNullOrEmpty(whereField) || whereValue == null ? null : $" WHERE {whereField} = @name";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand($"DELETE FROM {table}{where}", con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@name", whereValue));
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataDelete", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataDeleteWhereIn(string table, string field, List<int> list, bool not)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand($"DELETE FROM {table} where {field} {(not? "not": null)} in ({string.Join(",", list)})", con))
            {
                await con.OpenAsync();
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataDeleteWhereIn", ex, LogCat.SQLite);
                }
            }
        }



        #endregion

        #region Selection
        public async Task<List<TimerItem>> SQLiteDataSelectTimers()
        {
            var list = new List<TimerItem>();
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand("select * from timers", con))
            {
                await con.OpenAsync();
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var record = new TimerItem
                            {
                                id = r.GetInt32(0),
                                timerType = r.GetInt32(1),
                                timerStage = r.GetInt32(2),
                                timerLocation = r.GetString(3),
                                timerOwner = r.GetString(4),
                                timerET = r.GetString(5),
                                timerNotes = r.IsDBNull(6) ? null : r.GetString(6),
                                timerChar = r.GetString(7),
                                announce = r.GetInt32(8)
                            };
                            list.Add(record);
                        }

                        return list;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataUpdate", ex, LogCat.SQLite);
                    return new List<TimerItem>();
                }
            }
        }

        #endregion

        public async Task<bool> RunScript(string file)
        {
            if (!File.Exists(file)) return false;
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(File.ReadAllText(file), con))
            {
                await con.OpenAsync();
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("RunScript", ex, LogCat.SQLite);
                    return false;
                }

                return true;
            }
        }

        public async Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken)
        {
            if(string.IsNullOrEmpty(notifyToken) && string.IsNullOrEmpty(mailToken) || string.IsNullOrEmpty(userId)) return;

            var mail = string.IsNullOrEmpty(mailToken) ? await SQLiteDataQuery<string>("refreshTokens", "mail", "id", userId) : mailToken;
            var token = string.IsNullOrEmpty(notifyToken) ? await SQLiteDataQuery<string>("refreshTokens", "token", "id", userId) : notifyToken;

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand("INSERT OR REPLACE INTO refreshTokens (id, token, mail) VALUES(@id,@token,@mail)", con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@id", userId));
                insertSQL.Parameters.Add(new SqliteParameter("@token", token));
                insertSQL.Parameters.Add(new SqliteParameter("@mail", mail ?? (object)DBNull.Value));
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataUpdate", ex, LogCat.SQLite);
                }
            }
        }

        public async Task<IList<IDictionary<string, object>>> GetAuthUser(ulong uId, bool order = false)
        {
            var list = new List<IDictionary<string, object>>(); ;
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"SELECT * FROM authUsers WHERE discordID={uId}{(order? " ORDER BY addedOn DESC" : null)}", con))
            {
                await con.OpenAsync();
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var record = new Dictionary<string, object>();

                            for (var i = 0; i < r.FieldCount; i++)
                            {
                                var key = r.GetName(i);
                                var value = r.IsDBNull(i) ? null : r[i];
                                record.Add(key, value);
                            }

                            list.Add(record);
                        }

                        return list;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("GetAuthUser", ex, LogCat.SQLite);
                }
            }
            await Task.Yield();
            return list;
        }

        public async Task<List<IDictionary<string, object>>> GetPendingUser(string remainder)
        {
            var list = new List<IDictionary<string, object>>(); ;
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"SELECT * FROM pendingUsers WHERE authString=\"{remainder}\"", con))
            {
                await con.OpenAsync();
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var record = new Dictionary<string, object>();

                            for (var i = 0; i < r.FieldCount; i++)
                            {
                                var key = r.GetName(i);
                                var value = r[i];
                                record.Add(key, value);
                            }

                            list.Add(record);
                        }

                        return list;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("GetPendingUser", ex, LogCat.SQLite);
                }
            }
            await Task.Yield();
            return list;
            
        }

        public async Task RunCommand(string query2, bool silent = false)
        {
            try
            {
                using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
                using (var insertSQL = new SqliteCommand(query2, con))
                {
                    await con.OpenAsync();
                    insertSQL.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                    await LogHelper.LogEx($"[RunCommand]: {query2}", ex, LogCat.SQLite);
            }
        }

        public async Task<T> SQLiteDataSelectCache<T>(object whereValue, int maxDays)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand("SELECT text, lastUpdate FROM cache WHERE id=@value and type=@tt", con))
            {
                await con.OpenAsync();
                querySQL.Parameters.Add(new SqliteParameter("@value", whereValue));
                querySQL.Parameters.Add(new SqliteParameter("@tt", typeof(T).Name));
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        if (!r.HasRows) return (T)(object)null;

                        await r.ReadAsync();
                        //check for outdated cache
                        if ((DateTime.Now - r.GetDateTime(1)).Days >= maxDays)
                            return (T)(object)null;
                        var data = JsonConvert.DeserializeObject<T>(r.IsDBNull(0) ? null : r.GetString(0));
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataSelectCache", ex, LogCat.SQLite);
                    return (T)(object)null;
                }
            }
        }

        public async Task SQLiteDataUpdateCache<T>(T data, object id, int days = 1)
        {
            var entry = await SQLiteDataSelectCache<T>(id, int.MaxValue);
            var q = entry == null
                ? "INSERT OR REPLACE INTO cache (type, id, lastAccess, lastUpdate, text, days) VALUES(@type,@id,@access,@update,@text,@days)"
                : "UPDATE cache set lastAccess=@access, lastUpdate=@update where type=@type and id=@id";

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(q, con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@type", typeof(T).Name));
                insertSQL.Parameters.Add(new SqliteParameter("@id", id ?? DBNull.Value));
                insertSQL.Parameters.Add(new SqliteParameter("@access", DateTime.Now));
                insertSQL.Parameters.Add(new SqliteParameter("@update", DateTime.Now));
                insertSQL.Parameters.Add(new SqliteParameter("@days", days));
                if(entry == null)
                    insertSQL.Parameters.Add(new SqliteParameter("@text", JsonConvert.SerializeObject(data)));
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SQLiteDataUpdateCache]: {q}", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataPurgeCache()
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            {
                await con.OpenAsync();
                using (var q = new SqliteCommand("delete from cache where lastAccess <= date('now','-1 day') and days=1", con))
                {
                    try
                    {
                        q.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx("SQLiteDataPurgeCache", ex, LogCat.SQLite);
                    }
                }

                using (var q = new SqliteCommand("delete from cache where lastAccess <= date('now','-30 day') and days=30", con))
                {
                    try
                    {
                        q.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx("SQLiteDataPurgeCache", ex, LogCat.SQLite);
                    }
                }
            }
        }


        public async Task CleanupNotificationsList()
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            {
                await con.OpenAsync();
                using (var q = new SqliteCommand("delete from notificationsList where `time` <= date('now','-30 day')", con))
                {
                    try
                    {
                        q.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx("CleanupNotificationsList", ex, LogCat.SQLite);
                    }
                }
            }
        }
    }
}