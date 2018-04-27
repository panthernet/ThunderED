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
            "1.0.0"
        };

        public static async Task<bool> Upgrade()
        {
            var version = await SQLiteDataQuery("cacheData", "data", "name", "version");
            bool fullUpdate = string.IsNullOrEmpty(version);
            var vDbVersion = fullUpdate ? new Version(1,0,0) : new Version(version);
          //  var vAppVersion = new Version(Program.VERSION);

            try
            {
                foreach (var update in MajorVersionUpdates)
                {
                    var v = new Version(update);
                    if (vDbVersion >= v) continue;

                    switch (update)
                    {
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
        }
    }
}
