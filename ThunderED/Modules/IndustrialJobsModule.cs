using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public partial class IndustrialJobsModule: AppModuleBase
    {
        public override LogCat Category => LogCat.IndustryJobs;
        private int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<long, string> _etokens = new ConcurrentDictionary<long, string>();
        private readonly ConcurrentDictionary<long, string> _corpEtokens = new ConcurrentDictionary<long, string>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Industrial Jobs module...", Category);
            _checkInterval = Settings.ContractNotificationsModule.CheckIntervalInMinutes;
            if (_checkInterval == 0)
                _checkInterval = 1;

            await WebPartInitialization();

            var data = Settings.IndustrialJobsModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.CharacterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            _etokens.Clear();
            _corpEtokens.Clear();
        }

        public override async Task Run(object prm)
        {
            if (IsRunning || !Settings.Config.ModuleIndustrialJobs || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;
            try
            {
                if ((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Industrial Jobs module check...", Category);

                foreach (var (groupName, group) in Settings.IndustrialJobsModule.GetEnabledGroups())
                {
                    var chars = GetParsedCharacters(groupName) ?? new List<long>();
                    foreach (var characterID in chars)
                    {
                        var rtoken = await DbHelper.GetToken(characterID, TokenEnum.Industry);
                        if (rtoken == null)
                        {
                            await SendOneTimeWarning(characterID,
                                $"Industry jobs feed token for character {characterID} not found! User is not authenticated.");
                            continue;
                        }
                        if (rtoken.Scopes == null) continue;

                        var scope = new ESIScope();
                        if (SettingsManager.HasCharIndustryJobs(rtoken.Scopes.Split(',').ToList()))
                            scope.AddCharIndustry();
                        if (SettingsManager.HasCorpIndustryJobs(rtoken.Scopes.Split(',').ToList()))
                            scope.AddCorpIndustry();


                        var tq = await APIHelper.ESIAPI.GetAccessTokenWithScopes(rtoken, scope.Merge(), $"From {Category} | Char ID: {characterID}");
                        var token = tq.Result;
                        if (string.IsNullOrEmpty(token))
                        {
                            if (tq.Data.IsNotValid && !tq.Data.IsNoConnection)
                            {
                                await LogHelper.LogWarning(
                                    $"Industry token for character {characterID} is outdated or no more valid!");
                                await LogHelper.LogWarning($"Deleting invalid industry refresh token for {characterID}: {tq.Data.Message}", Category);
                                await DbHelper.DeleteToken(characterID, TokenEnum.Industry);
                            }
                            else
                                await LogHelper.LogWarning($"Unable to get industry token for character {characterID}. Current check cycle will be skipped. {tq.Data.ErrorCode}({tq.Data.Message})");

                            continue;
                        }

                        if (group.Filters.Any(a=> a.Value.FeedPersonalJobs))
                        {
                            await ProcessIndustryJobs(false, group, characterID, token);
                        }
                        if (group.Filters.Any(a=> a.Value.FeedCorporateJobs))
                        {
                            await ProcessIndustryJobs(true, group, characterID, token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false; 
            }
        }

        private async Task ProcessIndustryJobs(bool isCorp, IndustrialJobGroup group, long characterID, string token)
        {
            if(group == null) return;
            List<JsonClasses.IndustryJob> esiJobs;

            var corpID = isCorp ? (await APIHelper.ESIAPI.GetCharacterData(Reason, characterID))?.corporation_id ?? 0 : 0;
            
            if (isCorp)
            {
                var etag = _corpEtokens.GetOrNull(characterID);
                var result = await APIHelper.ESIAPI.GetCorpIndustryJobs(Reason, corpID, token, etag);
                _corpEtokens.AddOrUpdateEx(characterID, result?.Data?.ETag);
                if(result?.Data?.IsNotModified ?? true) return;
                esiJobs = result.Result?.OrderByDescending(a => a.job_id).ToList();
            }
            else
            {
                var etag = _etokens.GetOrNull(characterID);
                var result = await APIHelper.ESIAPI.GetCharacterIndustryJobs(Reason, characterID, token, etag);
                _etokens.AddOrUpdateEx(characterID, result?.Data?.ETag);
                if(result?.Data?.IsNotModified ?? true) return;
                esiJobs = result.Result?.OrderByDescending(a => a.job_id).ToList();
            }

            if (esiJobs == null || !esiJobs.Any())
                return;

            //ccp bug workaround
            var now = DateTime.UtcNow;
            foreach (var job in esiJobs.Where(a=> a.StatusValue == IndustryJobStatusEnum.active && a.end_date < now))
                job.status = "ready";

            var lastJobId = esiJobs.FirstOrDefault()?.job_id ?? 0;
            if (lastJobId == 0) return;

            var dbJobs = !isCorp ? await DbHelper.GetIndustryJobs(characterID, false) : await DbHelper.GetIndustryJobs(characterID, true);
            var dbJobsOther = isCorp ? await DbHelper.GetIndustryJobs(characterID, false) : null;//TODO check for sanity

            //check if initial startup
            if (dbJobs == null)
            {
                dbJobs = new List<JsonClasses.IndustryJob>(esiJobs.Where(a=> a.StatusValue != IndustryJobStatusEnum.delivered));
                //if (dbJobs.Any())
               // {
                await DbHelper.SaveIndustryJobs(characterID, dbJobs, isCorp);
                return;
                //}
            }


            // var ready = dbJobs.Where(a => a.StatusValue == IndustryJobStatusEnum.ready);
            // var ready2 = esiJobs.Where(a => a.StatusValue == IndustryJobStatusEnum.ready);
            //var x = dbJobs.FirstOrDefault(a => a.blueprint_type_id == 41607);


            //check db jobs
            foreach (var job in dbJobs.ToList())
            {
                var freshJob = esiJobs.FirstOrDefault(a => a.job_id == job.job_id);
                //check if job is present in esi, delete from db if not
                if (freshJob == null)
                {
                    dbJobs.Remove(job);
                    continue;
                }

                var filters = group.Filters.Count == 0
                    ? new Dictionary<string, IndustryJobFilter> {{"default", new IndustryJobFilter()}}
                    : group.Filters;
                foreach (var (filterName, filter) in filters)
                {
                    if(!CheckJobForFilter(filter, job, isCorp)) continue;

                    //check delivered
                    if (freshJob.StatusValue != job.StatusValue)
                    {
                        await SendDiscordMessage(freshJob, true, filter.DiscordChannels.Any() ? filter.DiscordChannels : @group.DiscordChannels, isCorp, token);
                        var index = dbJobs.IndexOf(job);
                        dbJobs.Remove(job);
                        dbJobs.Insert(index, freshJob);
                    }
                }

                //remove delivered job from db
                if (freshJob.StatusValue == IndustryJobStatusEnum.delivered)
                    dbJobs.Remove(job);
            }

            //silently remove filtered out expired contracts
            //probably not needed...
            dbJobs.RemoveAll(a => a.StatusValue == IndustryJobStatusEnum.delivered);

            //update cache list and look for new contracts
            var lastRememberedId = dbJobs?.FirstOrDefault()?.job_id ?? 0;
            if (lastJobId > lastRememberedId)
            {
                //get and report new jobs, forget already finished
                var newJobs = esiJobs.Where(a => a.job_id > lastRememberedId && a.StatusValue != IndustryJobStatusEnum.delivered).ToList();
                if (dbJobsOther != null)
                {
                    newJobs = newJobs.Where(a => dbJobsOther.All(b => b.job_id != a.job_id)).ToList();
                }

                //process new jobs
                foreach (var job in newJobs)
                {
                    foreach (var (filterName, filter) in @group.Filters)
                    {
                        if (!CheckJobForFilter(filter, job, isCorp)) continue;
                        await SendDiscordMessage(job, false, filter.DiscordChannels.Any() ? filter.DiscordChannels : @group.DiscordChannels, isCorp, token);
                    }
                }

                //add new jobs to db list
                if (newJobs.Count > 0)
                    dbJobs.InsertRange(0, newJobs);
            }

            //kill dupes
            var rr = dbJobs.GroupBy(a => a.job_id).Where(a => a.Count() > 1).Select(a=> a.Key).Distinct();
            foreach (var item in rr)
            {
                var o = dbJobs.FirstOrDefault(a => a.job_id == item);
                if (o != null)
                    dbJobs.Remove(o);
            }

            await DbHelper.SaveIndustryJobs(characterID, dbJobs, isCorp);
        }

        private bool CheckJobForFilter(IndustryJobFilter filter, JsonClasses.IndustryJob job, bool isCorp)
        {
            if (isCorp && !filter.FeedCorporateJobs) return false;
            if(!isCorp && !filter.FeedPersonalJobs) return false;

            if(!filter.FeedCancelledJobs && job.StatusValue == IndustryJobStatusEnum.cancelled) return false;
            if(!filter.FeedStartingJobs && job.StatusValue == IndustryJobStatusEnum.active) return false;
            if(!filter.FeedReadyJobs && job.StatusValue == IndustryJobStatusEnum.ready) return false;
            if(!filter.FeedDeliveredJobs && job.StatusValue == IndustryJobStatusEnum.delivered) return false;
            if(!filter.FeedRevertedJobs && job.StatusValue == IndustryJobStatusEnum.reverted) return false;
            if(!filter.FeedPausedJobs && job.StatusValue == IndustryJobStatusEnum.paused) return false;

            if (!filter.FeedResearchJobs && (job.Activity == IndustryJobActivity.me || job.Activity == IndustryJobActivity.te ||
                                             job.Activity == IndustryJobActivity.techResearch)) return false;
            if(!filter.FeedBuildJobs && job.Activity == IndustryJobActivity.build) return false;
            if(!filter.FeedCopyingJobs && job.Activity == IndustryJobActivity.copy) return false;
            if(!filter.FeedInventionJobs && job.Activity == IndustryJobActivity.inventing) return false;
            if(!filter.FeedReactionJobs && job.Activity == IndustryJobActivity.reaction) return false;

            return true;
        }

        private async Task SendDiscordMessage(JsonClasses.IndustryJob job, bool isStatusChange, List<ulong> discordChannels, bool isCorp, string token)
        {
            if(job == null) return;

            string statusText;
            string timeToComplete = null;
            switch (job.StatusValue)
            {
                case IndustryJobStatusEnum.active:
                    statusText = LM.Get("industryJobsStatusActive");
                    timeToComplete = TimeSpan.FromSeconds(job.duration).ToFormattedString(" ");
                    break;
                case IndustryJobStatusEnum.cancelled:
                    statusText = LM.Get("industryJobsStatusCancelled");
                    break;
                case IndustryJobStatusEnum.delivered:
                    statusText = LM.Get("industryJobsStatusDelivered");
                    break;
                case IndustryJobStatusEnum.paused:
                    statusText = LM.Get("industryJobsStatusPaused");
                    break;
                case IndustryJobStatusEnum.ready:
                    statusText = LM.Get("industryJobsStatusReady");
                    break;
                case IndustryJobStatusEnum.reverted:
                    statusText = LM.Get("industryJobsStatusReverted");
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown job status: {job.StatusValue}");
            }

            string activityText;
            switch (job.Activity)
            {
                case IndustryJobActivity.none:
                    activityText = LM.Get("industryJobsActivityNone");     
                    break;
                case IndustryJobActivity.build:
                    activityText = LM.Get("industryJobsActivityBuild");     
                    break;
                case IndustryJobActivity.techResearch:
                    activityText = LM.Get("industryJobsActivityTechResearch");     
                    break;
                case IndustryJobActivity.te:
                    activityText = LM.Get("industryJobsActivityTEResearch");     
                    break;
                case IndustryJobActivity.me:
                    activityText = LM.Get("industryJobsActivityMEResearch");     
                    break;
                case IndustryJobActivity.copy:
                    activityText = LM.Get("industryJobsActivityCopying");     
                    break;
                case IndustryJobActivity.duplicating:
                    activityText = LM.Get("industryJobsActivityDuplicating");     
                    break;
                case IndustryJobActivity.reverseEng:
                    activityText = LM.Get("industryJobsActivityReverseEng");     
                    break;
                case IndustryJobActivity.inventing:
                    activityText = LM.Get("industryJobsActivityInventing");     
                    break;
                case IndustryJobActivity.reaction:
                    activityText = LM.Get("industryJobsActivityReaction");     
                    break;
                case IndustryJobActivity.reaction2:
                    activityText = LM.Get("industryJobsActivityReaction");     
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown activity {job.activity_id}");
            }

            var bpType = await APIHelper.ESIAPI.GetTypeId(Reason, job.blueprint_type_id);
            var productType = await APIHelper.ESIAPI.GetTypeId(Reason, job.product_type_id);
            var completedBy = job.completed_character_id > 0 ? await APIHelper.ESIAPI.GetCharacterData(Reason, job.completed_character_id) : null;
            var unk = LM.Get("Unknown");
            var installer = job.installer_id > 0 ? await APIHelper.ESIAPI.GetCharacterData(Reason, job.installer_id) : null;

            var station = (await APIHelper.ESIAPI.GetStationData(Reason, job.facility_id, token))?.name ?? (await APIHelper.ESIAPI.GetUniverseStructureData(Reason, job.facility_id, token))?.name;

            var header = isStatusChange ? LM.Get("industryJobsStatusHeader") : LM.Get("industryJobsNewHeader");
            var sb = new StringBuilder();
            var color = isStatusChange ? null : "fix";
            var colorMark = string.Empty;
            var colorMove = string.Empty;
            if (job.StatusValue == IndustryJobStatusEnum.cancelled || job.StatusValue == IndustryJobStatusEnum.reverted)
            {
                color = "diff";
                colorMark = "-";
                colorMove= " ";
            }

            if (job.StatusValue == IndustryJobStatusEnum.ready || job.StatusValue == IndustryJobStatusEnum.delivered)
            {
                color = "diff";
                colorMark = "+";
                colorMove= " ";
            }

            sb.AppendLine($"```{color}");
            sb.AppendLine($"*{header}*");
            sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowActivity"),-11}: {activityText}");
            sb.AppendLine($"{colorMark}{LM.Get("industryJobsRowStatus"),-11}: {statusText}");
            
            if (job.Activity == IndustryJobActivity.build || job.Activity == IndustryJobActivity.inventing || job.Activity == IndustryJobActivity.reverseEng)
            {
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowFrom"),-11}: {bpType?.Name ?? unk}");
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowTo"),-11}: {productType?.Name ?? unk}");
            }
            else
            {
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowBPO"),-11}: {productType?.Name ?? unk}");
            }
            if (!string.IsNullOrEmpty(timeToComplete))
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowDuration"),-11}: {timeToComplete}");

            if(installer != null)
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowInstaller"),-11}: {installer.name}");
            if(completedBy != null && isCorp)
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowCompletedBy"),-11}: {completedBy.name}");
            sb.AppendLine($"{colorMark}{LM.Get("industryJobsRowStation"),-11}: {station ?? LM.Get("Unknown")}");
            sb.AppendLine("```");
            //sb.Append($"{LM.Get("industryJobsRowTime"),10}": {job.});
            foreach (var channel in discordChannels)
            {
                await APIHelper.DiscordAPI.SendMessageAsync(channel, sb.ToString());
            }
        }

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleIndustrialJobs) return false;
            var m = TickManager.GetModule<IndustrialJobsModule>();
            return m?.GetAllParsedCharacters().Contains(id) ?? false;
        }


    }
}
