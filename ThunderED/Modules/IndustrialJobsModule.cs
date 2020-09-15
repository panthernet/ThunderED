using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class IndustrialJobsModule: AppModuleBase
    {
        public override LogCat Category => LogCat.IndustryJobs;
        private readonly int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<long, string> _etokens = new ConcurrentDictionary<long, string>();
        private readonly ConcurrentDictionary<long, string> _corpEtokens = new ConcurrentDictionary<long, string>();

        public IndustrialJobsModule()
        {
            LogHelper.LogModule("Initializing IndustrialJobs module...", Category).GetAwaiter().GetResult();
            _checkInterval = Settings.ContractNotificationsModule.CheckIntervalInMinutes;
            if (_checkInterval == 0)
                _checkInterval = 1;
            WebServerModule.ModuleConnectors.Add(Reason, OnAuthRequest);
        }

        public override async Task Initialize()
        {
            var data = Settings.IndustrialJobsModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.CharacterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            _etokens.Clear();
            _corpEtokens.Clear();
        }

        private async Task<bool> OnAuthRequest(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleIndustrialJobs) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                RunningRequestCount++;
                var port = Settings.WebServerModule.WebExternalPort;

                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
                    if (request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{port}/callback")
                    {
                        var clientID = Settings.WebServerModule.CcpAppClientId;
                        var secret = Settings.WebServerModule.CcpAppSecret;
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        if (string.IsNullOrEmpty(state)) return false;

                        if (!state.StartsWith("ijobsauth")) return false;
                        //var groupName = HttpUtility.UrlDecode(state.Replace("ijobsauth", ""));

                        var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(
                                WebServerModule.GetAccessDeniedPage("Industry Jobs Module", LM.Get("accessDenied"),
                                    WebServerModule.GetAuthPageUrl()), response);
                            return true;
                        }

                        var lCharId = Convert.ToInt64(result[0]);
                        //var group = Settings.IndustrialJobsModule.Groups[groupName];
                        var allowedCharacters = GetAllParsedCharactersWithGroups();
                        string allowedGroup = null;
                        foreach (var (group, allowedCharacterIds) in allowedCharacters)
                        {
                            if (allowedCharacterIds.Contains(lCharId))
                            {
                                allowedGroup = group;
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(allowedGroup))
                        {
                            await WebServerModule.WriteResponce(
                                WebServerModule.GetAccessDeniedPage("Industry Jobs Module", LM.Get("accessDenied"),
                                    WebServerModule.GetAuthPageUrl()), response);
                            return true;
                        }

                        await SQLHelper.InsertOrUpdateTokens("", result[0], null, null, result[1]);
                        await WebServerModule.WriteResponce(File
                                .ReadAllText(SettingsManager.FileTemplateMailAuthSuccess)
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{header}", "authTemplateHeader")
                                .Replace("{body}", LM.Get("industryJobsAuthSuccessHeader"))
                                .Replace("{body2}", LM.Get("industryJobsAuthSuccessBody"))
                                .Replace("{backText}", LM.Get("backText")), response
                        );
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                RunningRequestCount--;
            }
            return false;
        }

        public override async Task Run(object prm)
        {
            if (IsRunning || !Settings.Config.ModuleIndustrialJobs) return;
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
                        var rtoken = await SQLHelper.GetRefreshTokenForIndustryJobs(characterID);
                        if (rtoken == null)
                        {
                            await SendOneTimeWarning(characterID, $"Industry jobs feed token for character {characterID} not found! User is not authenticated.");
                            continue;
                        }

                        var tq = await APIHelper.ESIAPI.RefreshToken(rtoken, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret, $"From {Category} | Char ID: {characterID}");
                        var token = tq.Result;
                        if (string.IsNullOrEmpty(token))
                        {
                            if (tq.Data.IsNotValid)
                                await LogHelper.LogWarning($"Industry token for character {characterID} is outdated or no more valid!");
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

            var dbJobs = !isCorp ? await SQLHelper.LoadIndustryJobs(characterID, false) : await SQLHelper.LoadIndustryJobs(characterID, true);
            var dbJobsOther = isCorp ? await SQLHelper.LoadIndustryJobs(characterID, false) : null;//TODO check for sanity

            //check if initial startup
            if (dbJobs == null)
            {
                dbJobs = new List<JsonClasses.IndustryJob>(esiJobs.Where(a=> a.StatusValue != IndustryJobStatusEnum.delivered));
                //if (dbJobs.Any())
               // {
                await SQLHelper.SaveIndustryJobs(characterID, dbJobs, isCorp);
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

            await SQLHelper.SaveIndustryJobs(characterID, dbJobs, isCorp);
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

            var station = (await APIHelper.ESIAPI.GetStationData(Reason, job.facility_id, token))?.name ?? (await APIHelper.ESIAPI.GetStructureData(Reason, job.facility_id, token))?.name;

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
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowFrom"),-11}: {bpType?.name ?? unk}");
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowTo"),-11}: {productType?.name ?? unk}");
            }
            else
            {
                sb.AppendLine($"{colorMove}{LM.Get("industryJobsRowBPO"),-11}: {productType?.name ?? unk}");
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
            return TickManager.GetModule<IndustrialJobsModule>().GetAllParsedCharacters().Contains(id);
        }
    }
}
