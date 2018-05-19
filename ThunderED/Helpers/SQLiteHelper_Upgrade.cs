using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ThunderED.Classes;

namespace ThunderED.Helpers
{
    public static partial class SQLiteHelper
    {
        private static readonly string[] MajorVersionUpdates = new[]
        {
            "1.0.0","1.0.1","1.0.7", "1.0.8", "*"
        };

        public static async Task<bool> Upgrade()
        {
            var version = await SQLiteDataQuery<string>("cacheData", "data", "name", "version");
            bool fullUpdate = string.IsNullOrEmpty(version);
            var vDbVersion = fullUpdate ? (SettingsManager.IsNew ? new Version(Program.VERSION) : new Version(1,0,0)) : new Version(version);
          //  var vAppVersion = new Version(Program.VERSION);

            try
            {
                foreach (var update in MajorVersionUpdates)
                {
                    if (update == "*")
                    {
                        await RunCommand("ALTER TABLE refreshTokens ADD mail TEXT NULL;", true);
                        continue;
                    }
                    var v = new Version(update);
                    if (vDbVersion >= v) continue;

                    switch (update)
                    {
                        case "1.0.1":
                            await RunCommand("DELETE FROM cacheData where name='version'");
                            await RunCommand("CREATE UNIQUE INDEX cacheData_name_uindex ON cacheData (name)");
                            await RunCommand("CREATE TABLE `killFeedCache` ( `type` text NOT NULL, `id` text NOT NULL, `lastId` TEXT)");
                            await RunCommand("CREATE UNIQUE INDEX killFeedCache_type_id_uindex ON killFeedCache (type, id)");
                            await RunCommand("delete from cache");
                            break;
                        case "1.0.7":
                            await RunCommand("CREATE TABLE `timersAuth` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP);");
                            await RunCommand(
                                "CREATE TABLE `timers` ( `id` INTEGER PRIMARY KEY NOT NULL, `timerType` int NOT NULL, `timerStage` int NOT NULL,`timerLocation` text NOT NULL, `timerOwner` text NOT NULL, `timerET` timestamp NOT NULL,`timerNotes` text, `timerChar` text NOT NULL, `announce` int NOT NULL DEFAULT 0);");
                            break;
                        case "1.0.8":
                            await RunCommand("ALTER TABLE refreshTokens ADD mail TEXT NULL;");
                            await RunCommand("CREATE TABLE `mail` ( `id` text UNIQUE PRIMARY KEY NOT NULL, `mailId` int DEFAULT 0);");
                            break;
                        default:
                            continue;
                    }
                }

                //update version in DB
                using (var con = new SqliteConnection($"Data Source = {SettingsManager.DatabaseFilePath};"))
                using (var insertSQL = new SqliteCommand("INSERT OR REPLACE INTO cacheData (name, data) VALUES(@id,@value)", con))
                {
                    await con.OpenAsync();
                    insertSQL.Parameters.Add(new SqliteParameter("@id", "version"));
                    insertSQL.Parameters.Add(new SqliteParameter("@value", Program.VERSION));
                    try
                    {
                        insertSQL.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx("Upgrade", ex, LogCat.SQLite);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("Upgrade", ex, LogCat.SQLite);
                return false;
            }
            finally
            {
                SettingsManager.IsNew = false;
            }
        }
    }
}
