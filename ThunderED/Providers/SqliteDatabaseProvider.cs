using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Providers
{
    internal class SqliteDatabaseProvider : DBProviderBase<SqliteConnection, SqliteCommand>, IDatabasePovider
    {
        #region Query

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

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value));
                }

                try
                {
                    using (var r = await command.ExecuteReaderAsync())
                    {
                        return r.HasRows;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[IsEntryExists]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.SQLite);
                    return false;
                }
            });  
        }

        public async Task<List<object[]>> SelectData(string table, string[] fields, Dictionary<string, object> where = null)
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

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value));
                }

                try
                {
                    using (var r = await command.ExecuteReaderAsync())
                    {
                        var list = new List<object[]>();
                        if (r.HasRows)
                        {
                            while (await r.ReadAsync())
                            {
                                var obj = new List<object>();

                                if (field == "*")
                                {
                                    for (int i = 0; i < r.VisibleFieldCount; i++)
                                    {
                                        obj.Add(r.IsDBNull(i) ? null : r.GetValue(i));
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < fields.Length; i++)
                                    {
                                        obj.Add(r.IsDBNull(i) ? null : r.GetValue(i));
                                    }
                                }

                                list.Add(obj.ToArray());
                            }
                        }
                        return list;
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[SelectData]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.SQLite);
                    return default;
                }

            });
        }

        public async Task<List<object[]>> SelectData(string query)
        {
            return await SessionWrapper(query, async command =>
            {
                try
                {
                    using (var r = await command.ExecuteReaderAsync())
                    {
                        var list = new List<object[]>();
                        if (r.HasRows)
                        {
                            while (await r.ReadAsync())
                            {
                                var obj = new List<object>();

                                for (int i = 0; i < r.VisibleFieldCount; i++)
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
                    await LogHelper.LogEx($"[{nameof(SelectData)}]: {query}", ex, LogCat.SQLite);
                    return default;
                }

            });
        }
      
        public async Task<T> Query<T>(string table, string field, Dictionary<string, object> where)
        {
            if (where == null)
            {
                await LogHelper.LogError($"[Query]: {table}-{field} query has empty values!");
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

            return await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in where)
                {
                    command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value));
                }
                try
                {
                    using (var r = await command.ExecuteReaderAsync())
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
                    await LogHelper.LogEx($"[Query]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                    return default;
                }

            });
        }

        #endregion
        
        #region Update

        public async Task InsertOrUpdate(string table, Dictionary<string, object> values)
        {
            if (values == null)
            {
                await LogHelper.LogError($"[{nameof(InsertOrUpdate)}]: {table} query has empty values!");
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
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(InsertOrUpdate)}]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            });
        }

        public async Task Insert(string table, Dictionary<string, object> values)
        {
            if (values == null)
            {
                await LogHelper.LogError($"[{nameof(Insert)}]: {table} query has empty values!");
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
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(Insert)}]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            });

        }

        public async Task Update(string table, string setField, object setData, Dictionary<string, object> where)
        {
            if (where == null)
            {
                await LogHelper.LogError($"[{nameof(Update)}]: {table} query has empty values!");
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
            await SessionWrapper(query, async command =>
            {
                count = 1;
                command.Parameters.Add(CreateParam<SqliteParameter>("@data", setData ?? DBNull.Value));
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                try
                {
                    command.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(Update)}]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.SQLite);
                }
            });

        }
        #endregion

        #region Delete
        public async Task Delete(string table, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"{pair.Key}=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"DELETE FROM {table} WHERE {whereText}";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam<SqliteParameter>($"@var{count++}", pair.Value));
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Delete), ex, LogCat.SQLite);
                }
            });
        }

        public async Task DeleteWhereIn(string table, string field, List<long> list, bool not)
        {
            var query = $"DELETE FROM {table} where {field} {(not ? "not" : null)} in ({string.Join(",", list)})";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(DeleteWhereIn), ex, LogCat.SQLite);
                }
            });
        }
        #endregion


        #region Cache

        public async Task<T> SelectCache<T>(object whereValue, int maxDays)
        {
            var query = "SELECT text, lastUpdate FROM cache WHERE id=@value and type=@tt";
            return await SessionWrapper(query, async command =>
            {
                command.Parameters.Add(CreateParam<SqliteParameter>("@value", whereValue));
                command.Parameters.Add(CreateParam<SqliteParameter>("@tt", typeof(T).Name));
                try
                {
                    using (var r = await command.ExecuteReaderAsync())
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
                    await LogHelper.LogEx(nameof(SelectCache), ex, LogCat.SQLite);
                    return (T)(object)null;
                }
            });
        }

        public async Task UpdateCache<T>(T data, object id, int days = 1)
        {
            var entry = await SelectCache<T>(id, int.MaxValue);
            var q = entry == null
                ? "INSERT OR REPLACE INTO cache (type, id, lastAccess, lastUpdate, text, days) VALUES(@type,@id,@access,@update,@text,@days)"
                : "UPDATE cache set lastAccess=@access, lastUpdate=@update where type=@type and id=@id";

            await SessionWrapper(q, async command =>
            {
                command.Parameters.Add(CreateParam<SqliteParameter>("@type", typeof(T).Name));
                command.Parameters.Add(CreateParam<SqliteParameter>("@id", id ?? DBNull.Value));
                command.Parameters.Add(CreateParam<SqliteParameter>("@access", DateTime.Now));
                command.Parameters.Add(CreateParam<SqliteParameter>("@update", DateTime.Now));
                command.Parameters.Add(CreateParam<SqliteParameter>("@days", days));
                if(entry == null)
                    command.Parameters.Add(CreateParam<SqliteParameter>("@text", JsonConvert.SerializeObject(data)));
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(UpdateCache)}]: {q}", ex, LogCat.SQLite);
                }
            });        
        }

        public async Task PurgeCache()
        {
            var query = "delete from cache where lastAccess <= date('now','-1 day') and days=1";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(PurgeCache), ex, LogCat.SQLite);
                }
            });

            query = "delete from cache where lastAccess <= date('now','-30 day') and days=30";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(PurgeCache), ex, LogCat.SQLite);
                }
            });
        }
        #endregion

        public async Task CleanupNotificationsList()
        {
            var query = "delete from notifications_list where `time` <= date('now','-30 day')";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("CleanupNotificationsList", ex, LogCat.SQLite);
                }
            });
        }

        protected override string CreateConnectString(bool skipDatabase = false)
        {
            if (!string.IsNullOrEmpty(SettingsManager.Settings.Database.CustomConnectionString))
                return SettingsManager.Settings.Database.CustomConnectionString;

            return $"Data Source = {SettingsManager.DatabaseFilePath};";
        }

        public async Task<bool> EnsureDBExists()
        {
            if (!File.Exists(SettingsManager.DatabaseFilePath))
            {
                File.Copy(Path.Combine(SettingsManager.RootDirectory, "edb.def.db"), SettingsManager.DatabaseFilePath);
                SettingsManager.IsNew = true;
            }

            await Task.Delay(1);
            return true;
        }
    }
}