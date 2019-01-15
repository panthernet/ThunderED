using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Providers
{
    internal class MysqlDatabaseProvider : IDatabasePovider
    {
        private static string _schema;
        private static string _schemaDot;

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

            var query = $"SELECT * FROM {_schemaDot}{table} {whereTrueText}";

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam($"@var{count++}", pair.Value));
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
                    whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
                }
            }

            var field = string.Join(',', fields.Select(a=> a == "*" ? a : $"`{a}`"));
            var whereTrueText = string.IsNullOrEmpty(whereText) ? null : $"WHERE {whereText}";

            var query = $"SELECT {field} FROM {_schemaDot}{table} {whereTrueText}";

            return await SessionWrapper(query, async command =>
            {
                if (!string.IsNullOrEmpty(whereText) && where != null)
                {
                    count = 1;
                    foreach (var pair in where)
                        command.Parameters.Add(CreateParam($"@var{count++}", pair.Value));
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
                    await LogHelper.LogEx($"[{nameof(SelectData)}]: {query} - {string.Join("|", where?.Select(a=> $"{a.Key}:{a.Value} ") ?? new List<string>())}", ex, LogCat.SQLite);
                    return default;
                }

            });
        }
      
        public async Task<T> Query<T>(string table, string field, Dictionary<string, object> where)
        {
            var value = (await SelectData(table, new[] {field}, where))?.FirstOrDefault()?.FirstOrDefault();
            var type = typeof(T);
            if (value == null)
                return default(T);
            if(type == typeof(string))
                return (T)value;
            if (type == typeof(int))
                return (T) (object) Convert.ToInt32(value);
            if (type == typeof(ulong))
                return (T) (object) Convert.ToUInt32(value);
            if (type == typeof(long))
                return (T) (object) Convert.ToInt64(value);
            return (T)value;
        }

        public async Task<List<T>> QueryList<T>(string table, string field, Dictionary<string, object> where)
        {
            return (await SelectData(table, new[] {field}, where))?.Select(a=> a.FirstOrDefault()).Cast<T>().ToList();
        }

        public async Task<T> Query<T>(string table, string field, string whereField, object whereData)
        {
            return await Query<T>(table, field, new Dictionary<string, object> {{whereField, whereData}});
        }

        public async Task<List<T>> QueryList<T>(string table, string field, string whereField, object whereData)
        {
            return await QueryList<T>(table, field, new Dictionary<string, object> {{whereField, whereData}});
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

            var query = $"REPLACE INTO {_schemaDot}{table} ({fromText}) values({valuesText})";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam($"@var{count++}", pair.Value ?? DBNull.Value));
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
                fromText += $"`{pair.Key}`{(pair.Key == last ? null : ",")}";
                valuesText += $"@var{count++}{(pair.Key == last ? null : ",")}";
            }

            var query = $"insert into {_schemaDot}{table} ({fromText}) values({valuesText})";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in values)
                {
                    command.Parameters.Add(CreateParam($"@var{count++}", pair.Value ?? DBNull.Value));
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

        public async Task Update(string table, string setField, object setData, string whereField, object whereData)
        {
            await Update(table, setField, setData, new Dictionary<string, object> {{whereField, whereData}});
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

            var query = $"UPDATE {_schemaDot}{table} SET `{setField}` = @data WHERE {whereText}";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                command.Parameters.Add(CreateParam("@data", setData ?? DBNull.Value));
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam($"@var{count++}", pair.Value ?? DBNull.Value));
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
                whereText += $"`{pair.Key}`=@var{count++}{(pair.Key == last? null : " and ")}";
            }

            var query = $"DELETE FROM {_schemaDot}`{table}` WHERE {whereText}";
            await SessionWrapper(query, async command =>
            {
                count = 1;
                foreach (var pair in where)
                    command.Parameters.Add(CreateParam($"@var{count++}", pair.Value));
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

        public async Task Delete(string table, string whereField = null, object whereValue = null)
        {
            var where = string.IsNullOrEmpty(whereField) || whereValue == null ? null : $" WHERE {whereField} = @name";
            var query = $"DELETE FROM {_schemaDot}`{table}`{where}";
            await SessionWrapper(query, async command =>
            {
                command.Parameters.Add(CreateParam("@name", whereValue));
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
            var query = $"DELETE FROM {_schemaDot}{table} where `{field}` {(not ? "not" : null)} in ({string.Join(",", list)})";
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

        #region Run

        public async Task<bool> RunScript(string file)
        {
            if (!File.Exists(file)) return false;

            return await SessionWrapper(File.ReadAllText(file), async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(RunScript), ex, LogCat.SQLite);
                    return false;
                }

                return true;
            });
        }

        public async Task<bool> RunScriptText(string text)
        {
            return await SessionWrapper(text, async command =>
            {
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(RunScriptText), ex, LogCat.SQLite);
                    return false;
                }

                return true;
            });
        }
      
        public async Task RunCommand(string query2, bool silent = false)
        {
            try
            {
                await SessionWrapper(query2, command =>
                {
                    command.ExecuteNonQuery();
                });
            }
            catch (Exception ex)
            {
                if (!silent)
                    await LogHelper.LogEx($"[{nameof(RunCommand)}]: {query2}", ex, LogCat.SQLite);
            }
        }

        public async Task RunSystemCommand(string query2, bool silent = false)
        {
            try
            {
                using (var session = new MySqlConnection(CreateConnectString(true)))
                {
                    using (var command = new MySqlCommand(query2, session))
                    {
                        await session.OpenAsync();
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                    await LogHelper.LogEx($"[{nameof(RunSystemCommand)}]: {query2}", ex, LogCat.SQLite);
            }
        }
        #endregion

        #region Cache

        public async Task<T> SelectCache<T>(object whereValue, int maxDays)
        {
            var query = $"SELECT `text`, `lastUpdate` FROM {_schemaDot}`cache` WHERE `id`=@value and `type`=@tt";
            return await SessionWrapper(query, async command =>
            {
                command.Parameters.Add(CreateParam("@value", whereValue));
                command.Parameters.Add(CreateParam("@tt", typeof(T).Name));
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
                ? $"REPLACE INTO {_schemaDot}`cache` (`type`, `id`, `lastAccess`, `lastUpdate`, `text`, `days`) VALUES(@type,@id,@access,@update,@text,@days)"
                : $"UPDATE {_schemaDot}`cache` set `lastAccess`=@access, `lastUpdate`=@update where `type`=@type and `id`=@id";

            await SessionWrapper(q, async command =>
            {
                command.Parameters.Add(CreateParam("@type", typeof(T).Name));
                command.Parameters.Add(CreateParam("@id", id ?? DBNull.Value));
                command.Parameters.Add(CreateParam("@access", DateTime.Now));
                command.Parameters.Add(CreateParam("@update", DateTime.Now));
                command.Parameters.Add(CreateParam("@days", days));
                if(entry == null)
                    command.Parameters.Add(CreateParam("@text", JsonConvert.SerializeObject(data)));
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
            var query = $"delete from {_schemaDot}`cache` where `lastAccess` <= DATE_SUB(NOW(), INTERVAL 1 DAY) and `days`=1";
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

            query = $"delete from {_schemaDot}`cache` where `lastAccess` <= DATE_SUB(NOW(), INTERVAL 30 DAY) and `days`=30";
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

        #region Session

        internal static async Task SessionWrapper(string query, Action<MySqlCommand> method)
        {
            using (var session = new MySqlConnection(CreateConnectString()))
            {
                using (var command = new MySqlCommand())
                {
                    command.CommandText = query;
                    command.Connection = session;
                    await session.OpenAsync();
                    method(command);
                }
            }
        }

        internal static async Task<T> SessionWrapper<T>(string query, Func<MySqlCommand, Task<T>> method)
        {
            using (var session = new MySqlConnection(CreateConnectString()))
            {
                using (var command = new MySqlCommand(query, session))
                {
                    await session.OpenAsync();
                    return await method(command);
                }
            }
        }

        private static string CreateConnectString(bool skipDatabase = false)
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
            sb.Append("SslMode=Preferred;");
            return sb.ToString();
        }

        private static MySqlParameter CreateParam(string name, object value)
        {
            return new MySqlParameter(name, value);
        }

        
        public async Task<bool> EnsureDBExists()
        {
            try
            {
                _schema = string.IsNullOrEmpty(SettingsManager.Settings.Database.DatabaseName) ? "ThunderED" : SettingsManager.Settings.Database.DatabaseName;
                _schemaDot = string.IsNullOrEmpty(SettingsManager.Settings.Database.DatabaseName) ? "ThunderED." : $"{SettingsManager.Settings.Database.DatabaseName}.";

                var query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{_schema}'";
                var result = false;
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
                    await LogHelper.LogError("Database not found in MySQL instance! Restore it manually using the dump file 'content/sql/mysql.dump'");
                    return false;
                    /* await LogHelper.LogWarning("Creating new database...");
                     await RunSystemCommand($"CREATE DATABASE IF NOT EXISTS {_schema}");
                     var text = File.ReadAllText(Path.Combine(SettingsManager.RootDirectory, "Content", "sql", "mysql_ddl.txt")).Replace("{schema}", _schemaDot);
                     if (!await RunScriptText(text))
                         return false;
                     await LogHelper.LogWarning("Filling DB with default data...");
                     text = File.ReadAllText(Path.Combine(SettingsManager.RootDirectory, "Content", "sql", "mysql_invTypes.txt")).Replace("{schema}", _schemaDot);
                     if (!await RunScriptText(text))
                         return false;
                     SettingsManager.IsNew = true;*/
                }

                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(EnsureDBExists), ex, LogCat.SQLite);
                return false;
            }
        }

        #endregion

        
        public async Task CleanupNotificationsList()
        {
            var query = "delete from notificationsList where `time` <= DATE_SUB(NOW(), INTERVAL 30 DAY)";
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
    }
}