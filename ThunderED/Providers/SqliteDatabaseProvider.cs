using System;
using System.Collections.Generic;
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

            
        public async Task<string> SQLiteDataQuery(string table, string field, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in @where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"SELECT {field} FROM {table} WHERE {whereText}", con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in @where)
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
                            return r.GetString(0) ?? "";
                        }
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataQuery", ex, LogCat.SQLite);
                    return null;
                }
            }
        }

        public async Task<string> SQLiteDataQuery(string table, string field, string whereField, object whereData)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"SELECT {field} FROM {table} WHERE {whereField} = @name", con))
            {
                await con.OpenAsync();
                querySQL.Parameters.Add(new SqliteParameter("@name", whereData));
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        if (r.HasRows)
                        {
                            await r.ReadAsync();
                            return r.GetString(0) ?? "";
                        }
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataQuery", ex, LogCat.SQLite);
                    return null;
                }
            }
        }

        public async Task<T> SQLiteDataQuery<T>(string table, string field, string whereField, object whereData)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"SELECT {field} FROM {table} WHERE {whereField} = @name", con))
            {
                await con.OpenAsync();
                querySQL.Parameters.Add(new SqliteParameter("@name", whereData));
                try
                {
                    using (var r = await querySQL.ExecuteReaderAsync())
                    {
                        if (r.HasRows)
                        {
                            await r.ReadAsync();
                            var type = typeof(T);
                            if(type == typeof(string))
                                return (T)(object)(r.GetString(0) ?? "");
                            if (type == typeof(int))
                                return (T) (object) r.GetInt32(0);
                        }
                        return default(T);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataQuery", ex, LogCat.SQLite);
                    return default(T);
                }
            }
        }

        #endregion
        
        //SQLite Update
        #region SQLiteUpdate

        public async Task SQLiteDataInsertOrUpdate(string table, Dictionary<string, object> values)
        {
            var fromText = string.Empty;
            var valuesText = string.Empty;
            int count = 1;
            var last = values.Keys.Last();
            foreach (var pair in values)
            {
                fromText += $"{pair.Key}{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var querySQL = new SqliteCommand($"insert or replace into {table} ({fromText}) values({valuesText})", con))
            {
                await con.OpenAsync();

                count = 1;
                foreach (var pair in values)
                {
                    querySQL.Parameters.Add(new SqliteParameter($"@var{count++}", pair.Value));
                }
                try
                {
                    querySQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataQuery", ex, LogCat.SQLite);
                }
            }
        }


        public async Task SQLiteDataUpdate(string table, string setField, object setData, string whereField, object whereData)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand($"UPDATE {table} SET {setField} = @data WHERE {whereField} = @name", con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@name", whereData));
                insertSQL.Parameters.Add(new SqliteParameter("@data", setData));
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
        #endregion

        //SQLite Delete
        #region SQLiteDelete
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
        #endregion


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
                                timerNotes = r.GetString(6),
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

        public async Task SQLiteDataInsertOrUpdateTokens(string notifyToken, string userId, string mailToken)
        {
            if(string.IsNullOrEmpty(notifyToken) && string.IsNullOrEmpty(mailToken) || string.IsNullOrEmpty(userId)) return;

            var mail = string.IsNullOrEmpty(mailToken) ? await SQLiteDataQuery("refreshTokens", "mail", "id", userId) : mailToken;
            var token = string.IsNullOrEmpty(notifyToken) ? await SQLiteDataQuery("refreshTokens", "token", "id", userId) : notifyToken;

            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand("INSERT OR REPLACE INTO refreshTokens (id, token, mail) VALUES(@id,@token,@mail)", con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@id", userId));
                insertSQL.Parameters.Add(new SqliteParameter("@token", token));
                insertSQL.Parameters.Add(new SqliteParameter("@mail", mail));
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


        public async Task InsertPendingUser(string characterID, string corporationid, string allianceid, string authString, string active, string dateCreated)
        {
            var query = "INSERT OR REPLACE INTO pendingUsers(characterID, corporationID, allianceID, authString, groups, active, dateCreated) " +
                        $"VALUES (\"{characterID}\", \"{corporationid}\", \"{allianceid}\", \"{authString}\", \"[]\", \"{active}\", \"{dateCreated}\")";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();
                try
                {
                    insertSQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("InsertPendingUser", ex, LogCat.SQLite);
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

        public async Task RunCommand(string query2)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(query2, con))
            {
                await con.OpenAsync();
                try
                {
                    insertSQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"RunCommand: {query2}", ex, LogCat.SQLite);
                }
            }
        }

        public async Task SQLiteDataInsertOrUpdateLastNotification(string characterID, string notifID)
        {
            var query = "INSERT OR REPLACE INTO notifications(characterID, lastNotificationID) " +
                        $"VALUES ('{characterID}', '{notifID}')";
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand(query, con))
            {
                await con.OpenAsync();
                try
                {
                    insertSQL.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("InsertPendingUser", ex, LogCat.SQLite);
                }
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
                        var data = JsonConvert.DeserializeObject<T>(r.GetString(0));
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

        public async Task SQLiteDataUpdateCacheField<T>(string setField, object setData, object whereId)
        {
            using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
            using (var insertSQL = new SqliteCommand($"UPDATE cache SET {setField} = @data WHERE id= @id and type=@tt", con))
            {
                await con.OpenAsync();
                insertSQL.Parameters.Add(new SqliteParameter("@data", setData));
                insertSQL.Parameters.Add(new SqliteParameter("@id", whereId));
                insertSQL.Parameters.Add(new SqliteParameter("@tt", typeof(T).Name));
                try
                {
                    insertSQL.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("SQLiteDataUpdateCache", ex, LogCat.SQLite);
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
                insertSQL.Parameters.Add(new SqliteParameter("@id", id));
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
                    await LogHelper.LogEx("SQLiteDataUpdateCache", ex, LogCat.SQLite);
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

    }
}