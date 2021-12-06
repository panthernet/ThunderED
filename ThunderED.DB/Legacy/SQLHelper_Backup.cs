using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ThunderED.Helpers;

namespace ThunderED
{
    public static partial class SQLHelper
    {
        private static Timer _backupTimer;
        private static string _backupDirectory;
        private static string _dbBackupFilenameFormat = "sqlite_db.{0}.bk";

        public static async Task InitializeBackup()
        {
            try
            {
                if (SettingsManager.Settings.Database.DatabaseProvider != "sqlite" || SettingsManager.Settings.Database.SqliteBackupFrequencyInHours == 0)
                    return;
                _backupTimer?.Dispose();
                _backupTimer = new Timer(10000);
                _backupTimer.Elapsed += BackupTimerOnElapsed;
                _backupDirectory = Path.Combine(SettingsManager.DataDirectory, "SqliteBackups");
                if (!Directory.Exists(_backupDirectory))
                    Directory.CreateDirectory(_backupDirectory);

                SettingsManager.Settings.Database.SqliteBackupMaxFiles =
                    SettingsManager.Settings.Database.SqliteBackupMaxFiles < 2 ? 2 : SettingsManager.Settings.Database.SqliteBackupMaxFiles;

                if (!await DbHelper.IsCacheDataExist("dbbackup_lasttime"))
                {
                    await DbHelper.UpdateCacheDataEntry("dbbackup_lasttime", DateTime.Now.ToString());
                    //await Insert("cache_data", new Dictionary<string, object> {{"name", "dbbackup_lasttime"}, {"data", DateTime.Now}});
                    await BackupDatabase(Path.Combine(_backupDirectory, string.Format(_dbBackupFilenameFormat, 1)));
                }

                _backupTimer.Start();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(InitializeBackup), ex, LogCat.Database);
            }
        }

        private static async void BackupTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            _backupTimer.Stop();
            try
            {
                var lastCheck = DateTime.Parse(await Query<string>("cache_data", "data", "name", "dbbackup_lasttime"));
                if (lastCheck.AddHours(SettingsManager.Settings.Database.SqliteBackupFrequencyInHours) < DateTime.Now)
                {
                    //await Update("cache_data", "data", DateTime.Now.ToString(), "name", "dbbackup_lasttime");
                    await DbHelper.UpdateCacheDataEntry("dbbackup_lasttime", DateTime.Now.ToString());
                    var fileNames = Directory.EnumerateFiles(_backupDirectory, string.Format(_dbBackupFilenameFormat, "*")).ToList();
                    if (!fileNames.Any())
                        await BackupDatabase(Path.Combine(_backupDirectory, string.Format(_dbBackupFilenameFormat, 1)));
                    else
                    {
                        var numbers = fileNames.Select(Path.GetFileName).Select(a => int.TryParse(a.Split('.')[1], out var result) ? result : 0).Distinct().ToList();
                        if (numbers.Count > SettingsManager.Settings.Database.SqliteBackupMaxFiles)
                        {
                            var countToDelete = fileNames.Count - SettingsManager.Settings.Database.SqliteBackupMaxFiles;
                            var data = numbers.OrderBy(a => a).Take(countToDelete);
                            foreach (var num in data)
                            {
                                try
                                {
                                    File.Delete(Path.Combine(_backupDirectory, string.Format(_dbBackupFilenameFormat, num)));
                                }
                                catch
                                {
                                    //ignore
                                }
                            }
                        }
                        var lastNumber = numbers.Any() ? numbers.Max() : 0;
                        await BackupDatabase(Path.Combine(_backupDirectory, string.Format(_dbBackupFilenameFormat, lastNumber + 1)));
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(BackupTimerOnElapsed), ex, LogCat.Database);
            }
            finally
            {
                _backupTimer.Start();
            }
        }
    }
}
