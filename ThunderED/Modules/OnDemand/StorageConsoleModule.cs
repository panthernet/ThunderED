using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public class StorageConsoleModule: AppModuleBase
    {
        public static LogCat Category2 => LogCat.StorageConsole;
        
        public override LogCat Category => Category2;

        //protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedListAccessLists = new Dictionary<string, Dictionary<string, List<long>>>();
        //protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedEditAccessLists = new Dictionary<string, Dictionary<string, List<long>>>();


        /*public override async Task Initialize()
        {
            ParsedListAccessLists.Clear();
            ParsedEditAccessLists.Clear();

            var data = new Dictionary<string, List<object>> { { "general", Settings.StorageConsoleModule.EditAccessEntities } };
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedEditAccessLists);
            data = new Dictionary<string, List<object>> { { "general", Settings.StorageConsoleModule.ListAccessEntities } };
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedListAccessLists);
        }

        #region Access

        public static bool HasAccess(long userId, long corpId = 0, long allyId = 0)
        {
            return HasListAccess(userId, corpId, allyId) || HasEditAccess(userId, corpId, allyId);
        }

        public static bool HasListAccess(long userId, long corpId = 0, long allyId = 0)
        {
            if (!SettingsManager.Settings.Config.ModuleStorageConsole) return false;
            var module = TickManager.GetModule<StorageConsoleModule>();
            return GetAllCharacterIds(module.).Contains(userId) ||
                   GetAllCorporationIds(module.ParsedListAccessLists).Contains(corpId) || (allyId > 0 &&
                       GetAllAllianceIds(module.ParsedListAccessLists).Contains(allyId));
        }

        public static bool HasEditAccess(long userId, long corpId = 0, long allyId = 0)
        {
            if (!SettingsManager.Settings.Config.ModuleStorageConsole) return false;
            var module = TickManager.GetModule<StorageConsoleModule>();
            return GetAllCharacterIds(module.ParsedEditAccessLists).Contains(userId) ||
                   GetAllCorporationIds(module.ParsedEditAccessLists).Contains(corpId) || (allyId > 0 &&
                       GetAllAllianceIds(module.ParsedEditAccessLists).Contains(allyId));
        }

        private static List<long> GetAllCharacterIds(Dictionary<string, Dictionary<string, List<long>>> dic)
        {
            return dic.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
        }
        private static List<long> GetAllCorporationIds(Dictionary<string, Dictionary<string, List<long>>> dic)
        {
            return dic.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
        }

        private static List<long> GetAllAllianceIds(Dictionary<string, Dictionary<string, List<long>>> dic)
        {
            return dic.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
        }

        #endregion
        */

        public static async Task<string> GetListCommandResult(string message)
        {
            try
            {
                message = message.Substring(4, message.Length - 4);
                var name = message.Length > 3 ? message : null;

                List<ThdStorageConsoleEntry> list;
                if (string.IsNullOrEmpty(name))
                    list = await DbHelper.GetStorageConsoleEntries();
                else list = new List<ThdStorageConsoleEntry> {await DbHelper.GetStorageConsoleEntry(name)};

                var sb = new StringBuilder();
                sb.Append(".\n```\n");
                if (list.Any())
                {
                    foreach (var entry in list)
                    {
                        sb.Append($"{entry.Name,-20}\t\t{entry.Value:N2}\n");
                    }
                }
                else
                {
                    sb.Append(LM.Get("scEmpty"));
                }

                sb.Append("```\n");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category2);
                return null;
            }
        }

        public static async Task<string> UpdateStorage(string message)
        {
            try
            {
                var command = message.Trim().Substring(0, 3);
                message = message.Substring(3, message.Length - 3).Trim();
                if(string.IsNullOrEmpty(message.Trim()))
                    return LM.Get("scInvalidCommand");

                string name = null;
                string value = null;
                if (message.StartsWith('"'))
                {
                    var lastIndex = message.LastIndexOf('"');
                    if (lastIndex > 0)
                    {
                        name = message.Substring(1, lastIndex - 1);
                        if (lastIndex + 1 < message.Length)
                            value = message.Substring(lastIndex + 1, message.Length - lastIndex- 1).Trim();
                    }
                }

                if (string.IsNullOrEmpty(name))
                {
                    var s = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    name = s[0].Trim();
                    value = s.Length == 1 ? "0" : s[1].Trim().Replace(",", ".");
                }

                var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ".";
                var hasValue = double.TryParse(value, NumberStyles.Any, ci, out var numericValue);

                switch (command.ToLower())
                {
                    case "set":
                        await DbHelper.SetStorageConsoleEntry(name, numericValue);
                        return LM.Get("scItemAdded", name);
                    case "add":
                        if (!hasValue)
                            return LM.Get("scInvalidCommand");
                        if (!await DbHelper.ModStorageConsoleEntry(name, numericValue))
                            return LM.Get("scItemNotFound");
                        break;
                    case "sub":
                        if (!hasValue)
                            return LM.Get("scInvalidCommand");
                        if(!await DbHelper.ModStorageConsoleEntry(name, -numericValue))
                            return LM.Get("scItemNotFound");
                        break;
                    case "del":
                        return !await DbHelper.RemoveStorageConsoleEntry(name) ? LM.Get("scItemNotFound") : LM.Get("scItemDeleted", name);
                    default:
                        throw new Exception($"Unknown command {command}");
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category2);
                return LM.Get("webFatalError");
            }
        }
    }
}
