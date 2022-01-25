using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dasync.Collections;

using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED.Modules.OnDemand
{
    public class FitCheckModule: AppModuleBase
    {
        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Fit Checker module...", Category);
            var data = Settings.FitCheckerModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.AccessEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            await base.Initialize();
        }

        public static async Task<bool> HasAccess(WebAuthUserData usr)
        {
            if (!SettingsManager.Settings.Config.ModuleFitChecker) return false;
            var module = TickManager.GetModule<FitCheckModule>();

            if (HasLiteAccess(usr.Id, usr.CorpId, usr.AllianceId, module))
                return true;

            var roles = await DiscordHelper.GetDiscordRoles(usr.Id);
            if (roles == null) return false;

            var allRoles = SettingsManager.Settings.FitCheckerModule.AccessGroups.Values.SelectMany(a => a.AccessDiscordRoles)
                .ToList();
            if (allRoles.Intersect(roles).Any())
                return true;
            return false;
        }

        private static bool HasLiteAccess(long id, long corpId, long allianceId, FitCheckModule module)
        {
            if (!SettingsManager.Settings.Config.ModuleFitChecker || TickManager.IsNoConnection || TickManager.IsESIUnreachable) return false;
            return module.GetAllParsedCharacters().Contains(id) || module.GetAllParsedCorporations().Contains(corpId) || (allianceId > 0 && module.GetAllParsedAlliances().Contains(allianceId));
        }



        public async Task<List<FitTargetGroupEntry>> GetTargetGroups()
        {
            return (await DbHelper.GetRegisteredUserCorpsAndAlliancesWithSkills()).OrderBy(a=> a.Name).ToList();
        }

        public async Task<ThdFit> ImportFit(string text, string group)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            try
            {
                var fit = new ThdFit {FitText = text, GroupName = @group};

                var equipmentList = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                var description = equipmentList[0];
                if (!description.StartsWith("["))
                    return null;
                description = description.Trim('[', ']');
                var splitDesc = description.Split(',');
                fit.ShipName = splitDesc[0].Trim();
                fit.Name = splitDesc[1].Trim();
                equipmentList.RemoveAt(0);
                equipmentList.Add(fit.ShipName);

                var data = await GetSkillsForTypes(equipmentList);
                if (data == null)
                    return null;
                fit.Skills = data;

                await DbHelper.SaveOrUpdateFit(fit);

                return fit;

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        private async Task AddOrUpdateFitResult(long? id, int value, List<FitSkillEntry> output)
        {
            if(!id.HasValue) return;

            var present = output.FirstOrDefault(a => a.Id == id.Value);
            if (present == null)
            {
                output.Add(new FitSkillEntry
                {
                    Id = id.Value,
                    Level = value,
                    Name = (await APIHelper.ESIAPI.GetTypeId(Reason, id.Value))?.Name
                });
            }
            else
            {
                if (present.Level < value)
                    present.Level = value;
            }
        }

        private List<string> PrepareInputFitStrings(List<string> names)
        {
            var result = new List<string>();
            foreach (var name in names)
            {
                var index = name.LastIndexOf(' ')+1;
                if (name[index] == 'x' || name[index] == 'х')
                    result.Add(name[..index].Trim());
                else result.Add(name);
            }

            return result.Distinct().ToList();
        }

        private async Task<List<FitSkillEntry>> GetSkillsForTypes(List<string> names)
        {
            var namesFixed = PrepareInputFitStrings(names);
            var types = await APIHelper.ESIAPI.GetUniverseIdsFromNames(Reason, namesFixed);
            var typeIds = types.inventory_types.Select(a => a.id);

            var output = new List<FitSkillEntry>();

            foreach (var id in typeIds)
            {
                var result = await APIHelper.ESIAPI.GetTypeId(Reason, id, true);
                if (result == null)
                    return null;
                var s1 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 182)?.value;
                var s2 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 183)?.value;
                var s3 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 184)?.value;
                var s1req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 277)?.value;
                var s2req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 278)?.value;
                var s3req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 279)?.value;

                var s4 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1285)?.value;
                var s5 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1289)?.value;
                var s6 = (long?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1290)?.value;
                var s4req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1286)?.value;
                var s5req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1287)?.value;
                var s6req = (int?)result.Attributes.FirstOrDefault(a => a.attribute_id == 1288)?.value;

                await AddOrUpdateFitResult(s1, s1req ?? 0, output);
                await AddOrUpdateFitResult(s2, s2req ?? 0, output);
                await AddOrUpdateFitResult(s3, s3req ?? 0, output);
                await AddOrUpdateFitResult(s4, s4req ?? 0, output);
                await AddOrUpdateFitResult(s5, s5req ?? 0, output);
                await AddOrUpdateFitResult(s6, s6req ?? 0, output);
            }

            return output;
        }

        public override LogCat Category => LogCat.FitCheck;

        public async Task<string> GetUserGroup(WebAuthUserData usr)
        {
            var roles = await DiscordHelper.GetDiscordRoles(usr.Id);

            foreach (var (key, value) in ParsedGroups)
            {
                if (value["character"].Contains(usr.Id) || value["corporation"].Contains(usr.CorpId) ||
                    (usr.AllianceId > 0 && value["alliance"].Contains(usr.AllianceId)))
                    return key;

                if (SettingsManager.Settings.FitCheckerModule.AccessGroups[key].AccessDiscordRoles.Any() && roles != null && roles.Any())
                {
                    if (roles.Intersect(SettingsManager.Settings.FitCheckerModule.AccessGroups[key].AccessDiscordRoles)
                        .Any())
                        return key;
                }
            }

            return null;
        }

        public async Task<string> ExecuteSearch(ThdFit fit, FitTargetGroupEntry targetGroup)
        {
            try
            {
                var userIds = (await DbHelper.GetUserInfoByTargetGroup(targetGroup));

                var validCount = 0;
                var names = new ConcurrentBag<string>();

                await userIds.ParallelForEachAsync(async info =>
                {
                    var token = await DbHelper.GetToken(info.Id, TokenEnum.General);
                    var accessToken = await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, new ESIScope().AddSkills().ToString());
                    if (accessToken == null || accessToken.Data.IsFailed)
                    {
                        if (accessToken != null && accessToken.Data.IsNotValid && !accessToken.Data.IsNoConnection)
                        {
                            await DbHelper.DeleteToken(info.Id, TokenEnum.General);
                            await LogHelper.LogWarning($"Deleting invalid token for {info.Id} {info.Name}", Category);
                            return;
                        }
                        await LogHelper.LogWarning($"Skipping fit check for {info.Id} {info.Name}", Category);
                        return;
                    }
                    var skills = await APIHelper.ESIAPI.GetCharSkills(Reason, info.Id, accessToken.Result);
                    if (skills == null)
                    {
                        await LogHelper.LogWarning($"Skipping fit check for {info.Id} {info.Name} (no skills)", Category);
                        return;
                    }

                    await foreach (var skill in fit.Skills)
                    {
                        var match = skills.skills.FirstOrDefault(a => a.skill_id == skill.Id);
                        if (match != null && match.active_skill_level >= skill.Level)
                            continue;
                        return;
                    }

                    validCount++;
                    names.Add(info.Name);
                }, Settings.Config.ConcurrentThreadsCount);

                var sb = new StringBuilder();
                sb.AppendLine(LM.Get("fitCheckReportHeader"));
                sb.AppendLine(LM.Get("fitCheckReportPilots",validCount, userIds.Count));
                sb.AppendLine("");
                if (names.Any())
                {
                    sb.AppendLine(LM.Get("fitCheckReportPilotNames"));
                    sb.AppendLine(string.Join('\n',names.OrderBy(a=> a)));
                    sb.AppendLine("");
                }

                sb.AppendLine(LM.Get("fitCheckReportFitDetails"));
                sb.AppendLine(string.Join('\n',fit.FitText.Split('\n', StringSplitOptions.RemoveEmptyEntries)));
                sb.AppendLine("");
                sb.AppendLine(LM.Get("fitCheckReportFitSkills"));
                await foreach (var skill in fit.Skills)
                    sb.AppendLine($"{skill.Level}  {skill.Name}");

                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine($"DEBUG All Chars {userIds.Count}");
                await foreach (var n in userIds.OrderBy(a=> a.Name))
                    sb.AppendLine(n.Name);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }
    }
}
