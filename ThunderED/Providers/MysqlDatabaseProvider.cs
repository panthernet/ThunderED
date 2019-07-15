using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Providers
{
    internal class MysqlDatabaseProvider : DBProviderBase<MySqlConnection, MySqlCommand>, IDatabasePovider
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
                    whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
                }
            }

            var whereTrueText = string.IsNullOrEmpty(whereText) ? null : $"WHERE {whereText}";

            var query = $"SELECT * FROM {SchemaDot}{table} {whereTrueText}";

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value));
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
                    await LogHelper.LogEx($"[IsEntryExists]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.Database);
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
                    whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
                }
            }

            var field = string.Join(',', fields.Select(a=> a == "*" ? a : $"`{a}`"));
            var whereTrueText = string.IsNullOrEmpty(whereText) ? null : $"WHERE {whereText}";

            var query = $"SELECT {field} FROM {SchemaDot}{table} {whereTrueText}";

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value));
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
                    await LogHelper.LogEx($"[{nameof(SelectData)}]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.Database);
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
                    await LogHelper.LogEx($"[{nameof(SelectData)}]: {query}", ex, LogCat.Database);
                    return default;
                }

            });
        }
      
        public async Task<T> Query<T>(string table, string field, Dictionary<string, object> where)
        {
            var value = (await SelectData(table, new[] {field}, where))?.FirstOrDefault()?.FirstOrDefault();
            var type = typeof(T);
            if (value == null)
                return default;
            if (type == typeof(string))
            {               
                return (T)(object)value.ToString();
            }

            if (type == typeof(int))
                return (T) (object) Convert.ToInt32(value);
            if (type == typeof(ulong))
                return (T) (object) Convert.ToUInt64(value);
            if (type == typeof(long))
                return (T) (object) Convert.ToInt64(value);
            return (T)value;
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
                fromText += $"`{pair.Key}`{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            var query = $"REPLACE INTO {SchemaDot}{table} ({fromText}) values({valuesText})";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(InsertOrUpdate)}]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.Database);
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
                fromText += $"`{pair.Key}`{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            var query = $"insert into {SchemaDot}{table} ({fromText}) values({valuesText})";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                }
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(Insert)}]: {query} - {string.Join("|", values.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.Database);
                }
            });

        }

        public async Task Update(string table, string setField, object setData)
        {
            var query = $"UPDATE {table} SET {setField} = @data";
            await SessionWrapper(query, async command =>
            {
                command.Parameters.Add(CreateParam<MySqlParameter>("@data", setData ?? DBNull.Value));
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(Update)}]: {query}", ex, LogCat.Database);
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
                whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"UPDATE {SchemaDot}{table} SET `{setField}` = @data WHERE {whereText}";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                command.Parameters.Add(CreateParam<MySqlParameter>("@data", setData ?? DBNull.Value));
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value ?? DBNull.Value));
                try
                {
                    command.ExecuteNonQuery();

                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(Update)}]: {query} - {string.Join("|", where.Select(a=> $"{a.Key}:{a.Value} "))}", ex, LogCat.Database);
                }
            });

        }
        #endregion

        #region Delete
        public async Task<bool> Delete(string table, Dictionary<string, object> where)
        {
            var whereText = string.Empty;
            int count = 1;
            var last = where.Keys.Last();
            foreach (var pair in where)
            {
                whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"DELETE FROM {SchemaDot}`{table}` WHERE {whereText}";
            return await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam<MySqlParameter>($"@var{count++}", pair.Value));
                try
                {
                    command.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Delete), ex, LogCat.Database);
                    return false;
                }
            });
        }

        public async Task DeleteWhereIn(string table, string field, List<long> list, bool not)
        {
            var query = $"DELETE FROM {SchemaDot}{table} where `{field}` {(not ? "not" : null)} in ({string.Join(",", list)})";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(DeleteWhereIn), ex, LogCat.Database);
                }
            });
        }
        #endregion

        #region Cache

        public async Task<T> SelectCache<T>(object whereValue, int maxDays)
        {
            var query = $"SELECT `text`, `lastUpdate` FROM {SchemaDot}`cache` WHERE `id`=@value and `type`=@tt";
            return await SessionWrapper(query, async command =>
            {
                command.Parameters.Add(CreateParam<MySqlParameter>("@value", whereValue));
                command.Parameters.Add(CreateParam<MySqlParameter>("@tt", typeof(T).Name));
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
                    await LogHelper.LogEx(nameof(SelectCache), ex, LogCat.Database);
                    return (T)(object)null;
                }
            });
        }

        public async Task UpdateCache<T>(T data, object id, int days = 1)
        {
            var entry = await SelectCache<T>(id, int.MaxValue);
            var q = entry == null
                ? $"REPLACE INTO {SchemaDot}`cache` (`type`, `id`, `lastAccess`, `lastUpdate`, `text`, `days`) VALUES(@type,@id,@access,@update,@text,@days)"
                : $"UPDATE {SchemaDot}`cache` set `lastAccess`=@access, `lastUpdate`=@update where `type`=@type and `id`=@id";

            await SessionWrapper(q, async command =>
            {
                command.Parameters.Add(CreateParam<MySqlParameter>("@type", typeof(T).Name));
                command.Parameters.Add(CreateParam<MySqlParameter>("@id", id ?? DBNull.Value));
                command.Parameters.Add(CreateParam<MySqlParameter>("@access", DateTime.Now));
                command.Parameters.Add(CreateParam<MySqlParameter>("@update", DateTime.Now));
                command.Parameters.Add(CreateParam<MySqlParameter>("@days", days));
                if(entry == null)
                    command.Parameters.Add(CreateParam<MySqlParameter>("@text", JsonConvert.SerializeObject(data)));
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"[{nameof(UpdateCache)}]: {q}", ex, LogCat.Database);
                }
            });        
        }

        public async Task PurgeCache()
        {
            var query = $"delete from {SchemaDot}`cache` where `lastAccess` <= DATE_SUB(NOW(), INTERVAL 1 DAY) and `days`=1";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(PurgeCache), ex, LogCat.Database);
                }
            });

            query = $"delete from {SchemaDot}`cache` where `lastAccess` <= DATE_SUB(NOW(), INTERVAL 30 DAY) and `days`=30";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(PurgeCache), ex, LogCat.Database);
                }
            });
        }
        #endregion

        #region Session

        protected override string CreateConnectString(bool skipDatabase = false)
        {
            if (!string.IsNullOrEmpty(SettingsManager.Settings.Database.CustomConnectionString))
                return SettingsManager.Settings.Database.CustomConnectionString;
            var sb = new StringBuilder();
            sb.Append($"Server={SettingsManager.Settings.Database.ServerAddress};");
            if(SettingsManager.Settings.Database.ServerPort > 0)
                sb.Append($"Port={SettingsManager.Settings.Database.ServerPort};");
            if(!skipDatabase)
                sb.Append($"Database={SettingsManager.Settings.Database.DatabaseName};");
            sb.Append($"Uid={SettingsManager.Settings.Database.UserId};");
            if(!string.IsNullOrEmpty(SettingsManager.Settings.Database.Password))
                sb.Append($"Pwd={SettingsManager.Settings.Database.Password};");
            //sb.Append("SslMode=Preferred;");
            return sb.ToString();
        }
        
        public async Task<bool> EnsureDBExists()
        {
            try
            {
                Schema = string.IsNullOrEmpty(SettingsManager.Settings.Database.DatabaseName) ? "ThunderED" : SettingsManager.Settings.Database.DatabaseName;
                SchemaDot = string.IsNullOrEmpty(SettingsManager.Settings.Database.DatabaseName) ? "ThunderED." : $"{SettingsManager.Settings.Database.DatabaseName}.";

                var query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{Schema}'";
                bool result;
                using (var session = new MySqlConnection(CreateConnectString(true)))
                {
                    using (var command = new MySqlCommand(query, session))
                    {
                        await session.OpenAsync();
                        using (var r = await command.ExecuteReaderAsync())
                            result = r.HasRows;
                    }
                }

                if (!result)
                {
                    await LogHelper.LogError("Database not found in MySQL instance! Restore it manually using the dump file 'mysql.dump'");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(EnsureDBExists), ex, LogCat.Database);
                return false;
            }
        }



        #endregion

        public async Task CleanupNotificationsList()
        {
            var query = "delete from notifications_list where `time` <= DATE_SUB(NOW(), INTERVAL 30 DAY)";
            await SessionWrapper(query, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("CleanupNotificationsList", ex, LogCat.Database);
                }
            });
        }

        public async Task<List<object[]>> SelectDataWithDateCondi(string table, string[] fields, string whereField, int minutes, int limit)
        {
            var field = string.Join(',', fields);
            var query = $"SELECT {field} FROM {table} WHERE authState=2 and main_character_id is null and (`{whereField}` is null or `{whereField}` <= DATE_SUB(NOW(), INTERVAL {minutes} MINUTE)) LIMIT {limit}";

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
                    await LogHelper.LogEx($"[SelectDataWithDateCondi]: {query} ", ex, LogCat.Database);
                    return default;
                }

            });
        }
    }
}