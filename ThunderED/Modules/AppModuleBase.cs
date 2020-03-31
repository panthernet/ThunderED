using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public abstract class AppModuleBase
    {
        public string Reason => Category.ToString();
        public bool LogToConsole = true;
        public abstract LogCat Category { get; }

        protected bool IsFirstPass { get; private set; } = true;

        protected void InvalidateFirstPass()
        {
            IsFirstPass = false;
        }

        public virtual void Cleanup() {}

        /// <summary>
        /// List of IDs to control one-time warnings during the single bot session
        /// </summary>
        private readonly List<object> _oneTimeWarnings = new List<object>();

        /// <summary>
        /// Returns True internal subroutine is running
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// Returns True internal web request processing is running
        /// </summary>
        public bool IsRequestRunning => RunningRequestCount > 0;

        protected volatile int RunningRequestCount;

        internal async Task RunInternal(object prm)
        {
            if (Program.IsClosing) return;
            await Run(prm);
        }

        public virtual Task Run(object prm)
        {
            return Task.CompletedTask;
        }

        protected async Task SendOneTimeWarning(object id, string message)
        {
            if (_oneTimeWarnings.Contains(id)) return;

            await LogHelper.LogWarning(message, Category);
            _oneTimeWarnings.Add(id);
        }

        public virtual Task Initialize()
        {
            return Task.CompletedTask;
        }

        public ThunderSettings Settings => SettingsManager.Settings;

        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedGroups = new Dictionary<string, Dictionary<string, List<long>>>();

        protected virtual List<long> GetParsedSolarSystems(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("solar_system") ? null : storage[groupName]["solar_system"]);
        }
        protected virtual List<long> GetParsedConstellations(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("constellation") ? null : storage[groupName]["constellation"]);
        }

        protected virtual List<long> GetParsedRegions(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("region") ? null : storage[groupName]["region"]);
        }

        protected virtual List<long> GetParsedCharacters(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("character") ? null : storage[groupName]["character"]);
        }

        protected virtual List<long> GetAllParsedCharacters(Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return storage.Where(a=> a.Value.ContainsKey("character")).SelectMany(a=> a.Value["character"]).Distinct().ToList();
        }

        protected virtual Dictionary<string, List<long>> GetAllParsedCharactersWithGroups(Dictionary<string, Dictionary<string, List<long>>> storage = null)
        {
            storage = storage ?? ParsedGroups;
            return storage.Where(a => a.Value.ContainsKey("character"))
                .SelectMany(a => new Dictionary<string, List<long>> {{a.Key, a.Value["character"]}})
                .ToDictionary(a => a.Key, a => a.Value);
        }


        protected virtual List<long> GetParsedCorporations(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("corporation") ? null : storage[groupName]["corporation"]);
        }

        protected virtual List<long> GetAllParsedCorporations(Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return storage.Where(a=> a.Value.ContainsKey("corporation")).SelectMany(a=> a.Value["corporation"]).Distinct().ToList();
        }

        protected virtual List<long> GetParsedAlliances(string groupName, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return !storage.ContainsKey(groupName) ? null : (!storage[groupName].ContainsKey("alliance") ? null : storage[groupName]["alliance"]);
        }

        protected virtual List<long> GetAllParsedAlliances(Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            return storage.Where(a=> a.Value.ContainsKey("alliance")).SelectMany(a=> a.Value["alliance"]).Distinct().ToList();
        }

        protected virtual async Task ParseMixedDataArray(Dictionary<string, List<object>> data, MixedParseModeEnum mode, Dictionary<string, Dictionary<string, List<long>>>  storage = null)
        {
            storage = storage ?? ParsedGroups;
            storage.Clear();
            foreach (var groupPair in data)
            {
                switch (mode)
                {
                    case MixedParseModeEnum.Location:
                    {
                        var result = await ParseLocationDataArray(groupPair.Value);
                        storage.Add(groupPair.Key, result);                           
                    }
                        continue;
                    case MixedParseModeEnum.Member:
                    {
                        var result = await ParseMemberDataArray(groupPair.Value);
                        storage.Add(groupPair.Key, result);                           
                    }
                        continue;
                }
            }
        }

        /// <summary>
        /// Gets or sets if there was errors during the initial entity queries
        /// </summary>
        public volatile bool IsEntityInitFailed;

        protected async Task<Dictionary<string, List<long>>> ParseMemberDataArray(List<object> list)
        {
            var result = new Dictionary<string, List<long>> {{"character", new List<long>()}, {"corporation", new List<long>()}, {"alliance", new List<long>()}};
            if (!list.Any()) return result;
            var failedList = new List<object>();
            foreach (var entity in list)
            {
                try
                {
                    if (entity == null) continue;
                    if (long.TryParse(entity.ToString(), out var longNumber))
                    {
                        if(Settings.Config.ExtendedESILogging)
                            await LogHelper.LogInfo($"Resolving character {entity}...");

                        var rCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, entity, false, false, true);
                        if (rCharacter != null)
                            result["character"].Add(longNumber);
                        else
                        {
                            if (Settings.Config.ExtendedESILogging)
                                await LogHelper.LogInfo($"Resolving corp {entity}...");
                            var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, entity, false, false, true);
                            if (rCorp != null)
                                result["corporation"].Add(longNumber);
                            else
                            {
                                if (Settings.Config.ExtendedESILogging)
                                    await LogHelper.LogInfo($"Resolving alliance {entity}...");
                                var rAlliance = await APIHelper.ESIAPI.GetAllianceData(Reason, entity, false, false, true);
                                if (rAlliance != null)
                                    result["alliance"].Add(longNumber);
                                else failedList.Add(longNumber);
                            }
                        }
                    }
                    else
                    {
                        var str = entity.ToString();
                        var mod = str.Length > 2 ? str.Substring(0, 2).ToLower() : null;
                        var enumMod = mod == "a:" ? MemberSearchModeEnum.Alliance : (mod == "c:" ? MemberSearchModeEnum.Corporation : MemberSearchModeEnum.Character);
                        var searchString = str.StartsWith("c:") || str.StartsWith("a:") ? str.Remove(0, 2) : str;

                        var res = await APIHelper.ESIAPI.SearchMemberEntity(Reason, searchString, true);
                        if (res != null)
                        {
                            if (res.character.Any() && enumMod == MemberSearchModeEnum.Character)
                                result["character"].Add(res.character.First());
                            else if (res.corporation.Any() && enumMod == MemberSearchModeEnum.Corporation)
                                result["corporation"].Add(res.corporation.First());
                            else if (res.alliance.Any() && enumMod == MemberSearchModeEnum.Alliance)
                                result["alliance"].Add(res.alliance.First());
                            else failedList.Add(searchString);
                        }else failedList.Add(searchString);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }

            if (failedList.Any())
            {
                IsEntityInitFailed = true;
                await LogHelper.LogWarning($"Member entities not found: {string.Join(',', failedList)}!");
            }

            return result;
        }

        protected async Task<Dictionary<string, List<long>>> ParseLocationDataArray(List<object> list)
        {
            var result = new Dictionary<string, List<long>> {{"solar_system", new List<long>()}, {"constellation", new List<long>()}, {"region", new List<long>()}};
            if (!list.Any()) return result;
            var failedList = new List<object>();
            foreach (var entity in list)
            {
                try
                {
                    if (entity == null) continue;
                    if (long.TryParse(entity.ToString(), out var longNumber))
                    {
                        //got ID
                        var rSystem = await APIHelper.ESIAPI.GetSystemData(Reason, entity);
                        if (rSystem != null)
                            result["solar_system"].Add(longNumber);
                        else
                        {
                            var rConst = await APIHelper.ESIAPI.GetConstellationData(Reason, entity);
                            if (rConst != null)
                                result["constellation"].Add(longNumber);
                            else
                            {
                                var rRegion = await APIHelper.ESIAPI.GetRegionData(Reason, entity);
                                if (rRegion != null)
                                    result["region"].Add(longNumber);
                                else failedList.Add(longNumber);
                            }
                        }
                    }
                    else
                    {
                        var res = await APIHelper.ESIAPI.SearchLocationEntity(Reason, entity.ToString());
                        if (res != null)
                        {
                            if (res.solar_system.Any())
                                result["solar_system"].Add(res.solar_system.First());
                            else if (res.constellation.Any())
                                result["constellation"].Add(res.constellation.First());
                            else if (res.region.Any())
                                result["region"].Add(res.region.First());
                            else failedList.Add(entity);
                        }
                        else failedList.Add(entity);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }

            if (failedList.Any())
                await LogHelper.LogWarning($"Location entities not found: {string.Join(',', failedList)}!");
            return result;
        }

        protected async Task<Dictionary<string, List<long>>> ParseTypeDataArray(List<object> list)
        {
            var result = new Dictionary<string, List<long>> {{"type", new List<long>()}};
            if (!list.Any()) return result;
            var failedList = new List<object>();
            foreach (var entity in list)
            {
                try
                {
                    if (entity == null) continue;
                    if (long.TryParse(entity.ToString(), out var longNumber))
                    {
                        //got ID
                        var rType = await APIHelper.ESIAPI.GetTypeId(Reason, longNumber);
                        if (rType != null)
                            result["type"].Add(longNumber);
                        else failedList.Add(longNumber);
                    }
                    else
                    {
                        var res = await APIHelper.ESIAPI.SearchTypeEntity(Reason, entity.ToString());
                        if (res != null)
                        {
                            if (res.inventory_type.Any())
                                result["type"].Add(res.inventory_type.First());
                            else failedList.Add(entity);
                        }
                        else failedList.Add(entity);
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }

            if (failedList.Any())
                await LogHelper.LogWarning($"Type entities not found: {string.Join(',', failedList)}!");
            return result;
        }

        #region Tier2 Entity methods
        protected List<long> GetTier2CharacterIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "character", group, filter);
        }

        public List<long> GetTier2CorporationIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "corporation", group, filter);
        }

        protected List<long> GetTier2AllianceIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "alliance", group, filter);
        }

        protected List<long> GetTier2SystemIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "solar_system", group, filter);
        }

        protected List<long> GetTier2ConstellationIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "constellation", group, filter);
        }

        protected List<long> GetTier2RegionIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "region", group, filter);
        }

        protected List<long> GetTier2TypeIds(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string group = null, string filter = null)
        {
            return GetFromTier2Dictionary(dic, "type", group, filter);
        }

        protected List<long> GetFromTier2Dictionary(Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> dic, string prm, string group = null,
            string filter = null)
        {
            if(group == null)
                return dic.SelectMany(a=> a.Value).Where(a => a.Value.ContainsKey(prm)).SelectMany(a => a.Value[prm]).Distinct().Where(a => a > 0).ToList();
            if(!dic.ContainsKey(group) && !dic.Values.Any(a=> a.ContainsKey(filter))) return new List<long>();
            return dic[group][filter].Where(a => a.Key.Equals(prm, StringComparison.OrdinalIgnoreCase)).SelectMany(a => a.Value).Distinct().Where(a => a > 0).ToList();
        }

        #endregion

        protected enum MixedParseModeEnum
        {
            Location,
            Member
        }
        protected enum MemberSearchModeEnum
        {
            Character,
            Corporation,
            Alliance,
            None
        }
    }

}