using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public class MoonInfoModule: AppModuleBase
    {
        public override LogCat Category => LogCat.MoonInfo;

        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedViewAccessLists = new Dictionary<string, Dictionary<string, List<long>>>();
        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedLimitedAccessLists = new Dictionary<string, Dictionary<string, List<long>>>();


        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Moon Info module...", Category);

            ParsedViewAccessLists.Clear();
            ParsedLimitedAccessLists.Clear();

            var data = new Dictionary<string, List<object>> { {"general", Settings.MoonTableModule.ViewAccessEntities } }; 
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedViewAccessLists);
            data = new Dictionary<string, List<object>> { { "general", Settings.MoonTableModule.LimitedAccessEntities } };
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedLimitedAccessLists);

        }

        public async Task UpdateMoonTable(ThdMoonTableEntry entry)
        {
            if (entry.RegionId == 0)
                entry.RegionId = (await APIHelper.ESIAPI.GetSystemData("MoonInfo", entry.SystemId))?.DB_RegionId ?? 0;
            if (string.IsNullOrEmpty(entry.OreName))
                entry.OreName = (await APIHelper.ESIAPI.GetTypeId("MoonInfo", entry.OreId))?.name;
            await DbHelper.UpdateMoonTable(entry);
        }

        public async Task<List<ThdMoonTableEntry>> UpdateMoonTable(List<ThdMoonTableEntry> list)
        {
            foreach (var entry in list)
            {
                if (entry.RegionId == 0)
                    entry.RegionId = (await APIHelper.ESIAPI.GetSystemData("MoonInfo", entry.SystemId))?.DB_RegionId ??
                                     0;
                if (string.IsNullOrEmpty(entry.OreName))
                    entry.OreName = (await APIHelper.ESIAPI.GetTypeId("MoonInfo", entry.OreId))?.name;
            }

            return await DbHelper.UpdateMoonTable(list);

        }

        #region Access

        public static bool HasAccess(WebAuthUserData user)
        {
            return HasViewAccess(user) || HasLimitedAccess(user);
        }

        public static bool HasViewAccess(WebAuthUserData user)
        {
            if (!SettingsManager.Settings.Config.ModuleMoonTable) return false;
            var module = TickManager.GetModule<MoonInfoModule>();
            return GetAllCharacterIds(module.ParsedViewAccessLists).Contains(user.Id) ||
                   GetAllCorporationIds(module.ParsedViewAccessLists).Contains(user.CorpId) || (user.AllianceId > 0 &&
                       GetAllAllianceIds(module.ParsedViewAccessLists).Contains(user.AllianceId));
        }

        public static bool HasLimitedAccess(WebAuthUserData user)
        {
            if (!SettingsManager.Settings.Config.ModuleMoonTable) return false;
            var module = TickManager.GetModule<MoonInfoModule>();
            return GetAllCharacterIds(module.ParsedLimitedAccessLists).Contains(user.Id) ||
                   GetAllCorporationIds(module.ParsedLimitedAccessLists).Contains(user.CorpId) || (user.AllianceId > 0 &&
                       GetAllAllianceIds(module.ParsedLimitedAccessLists).Contains(user.AllianceId));
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

        public async Task<MoonUploadResult> CheckUploadString(string inputText)
        {
            var r = new MoonUploadResult();
            try
            {
                var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ".";

                if (string.IsNullOrEmpty(inputText))
                {
                    r.Error = LM.Get("webInputIsEmpty");
                    return r;
                }

                var lines = inputText.Split("\n").ToList();
                var chk = lines.Count > 0 && lines[0].Split('\t', StringSplitOptions.RemoveEmptyEntries).Length == 7;
                if(!chk)
                {
                    r.Error = LM.Get("webInvalidInputFormat");
                    return r;
                }
                //skip headers
                lines.RemoveAt(0);
                //var body = false;
                var currentMoonName = string.Empty;

                foreach (var line in lines)
                {
                    var data = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if(data.Length == 0) continue;

                    if (data.Length == 1)
                    {
                        currentMoonName = data[0].RemoveLocalizedTag();
                        continue;
                    }

                    if (data.Length < 6)
                    {
                        r.List.Clear();
                        r.Error = LM.Get("webInvalidInputFormat");
                        return r;
                    }

                    var entry = new ThdMoonTableEntry
                    {
                        MoonId = Convert.ToInt64(data[5]),
                        PlanetId = Convert.ToInt64(data[4]),
                        SystemId = Convert.ToInt64(data[3]),
                        OreId = Convert.ToInt64(data[2]),
                        OreQuantity = double.Parse(data[1], NumberStyles.Any, ci),
                        OreName = data[0].RemoveLocalizedTag(),
                        MoonName = currentMoonName
                    };
                    r.List.Add(entry);
                }

            }
            catch(Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                r.List.Clear();
                r.Error = LM.Get("webFatalError");
            }

            return r;
        }
    }

    public class MoonUploadResult
    {
        public List<ThdMoonTableEntry> List = new List<ThdMoonTableEntry>();
        public string Error;
    }
}
