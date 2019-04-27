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

        /// <summary>
        /// List of IDs to control one-time warnings during the single bot session
        /// </summary>
        private readonly List<object> _oneTimeWarnings = new List<object>();

        public bool IsRunning { get; set; }

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

        public virtual async Task Initialize()
        {

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

        private async Task<Dictionary<string, List<long>>> ParseMemberDataArray(List<object> list)
        {
            var result = new Dictionary<string, List<long>> {{"character", new List<long>()}, {"corporation", new List<long>()}, {"alliance", new List<long>()}};
            if (!list.Any()) return result;
            foreach (var entity in list)
            {
                try
                {
                    if (entity == null) continue;
                    if (long.TryParse(entity.ToString(), out var longNumber))
                    {
                        var rCharacter = await APIHelper.ESIAPI.GetCharacterData(Reason, entity);
                        if (rCharacter != null)
                            result["character"].Add(longNumber);
                        else
                        {
                            var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, entity);
                            if (rCorp != null)
                                result["corporation"].Add(longNumber);
                            else
                            {
                                var rAlliance = await APIHelper.ESIAPI.GetAllianceData(Reason, entity);
                                if (rAlliance != null)
                                    result["alliance"].Add(longNumber);
                            }
                        }
                    }
                    else
                    {
                        var str = entity.ToString();
                        var mod = str.Length > 2 ? str.Substring(0, 2).ToLower() : null;
                        var enumMod = mod == "a:" ? MemberSearchModeEnum.Alliance : (mod == "c:" ? MemberSearchModeEnum.Corporation : MemberSearchModeEnum.Character);
                        var searchString = str.StartsWith("c:") || str.StartsWith("a:") ? str.Remove(0, 2) : str;

                        var res = await APIHelper.ESIAPI.SearchMemberEntity(Reason, searchString);
                        if (res != null)
                        {
                            if (res.character.Any() && enumMod == MemberSearchModeEnum.Character)
                                result["character"].Add(res.character.First());
                            if (res.corporation.Any() && enumMod == MemberSearchModeEnum.Corporation)
                                result["corporation"].Add(res.corporation.First());
                            if (res.alliance.Any() && enumMod == MemberSearchModeEnum.Alliance)
                                result["alliance"].Add(res.alliance.First());
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }

            return result;
        }

        private async Task<Dictionary<string, List<long>>> ParseLocationDataArray(List<object> list)
        {
            var result = new Dictionary<string, List<long>> {{"solar_system", new List<long>()}, {"constellation", new List<long>()}, {"region", new List<long>()}};
            if (!list.Any()) return result;
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
                            if (res.constellation.Any())
                                result["constellation"].Add(res.constellation.First());
                            if (res.region.Any())
                                result["region"].Add(res.region.First());
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx(nameof(Initialize), ex, Category);
                }
            }

            return result;
        }

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