using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.Internal;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public partial class TimersModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Timers;

        public TimersModule()
        {
            LogHelper.LogModule("Initializing Timers module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnDisplayTimers);
        }
        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedAccessLists = new Dictionary<string, Dictionary<string, List<long>>>();
        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedEditLists = new Dictionary<string, Dictionary<string, List<long>>>();

        public List<long> GetAllCharacterIds()
        {
            return ParsedAccessLists.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
        }
        public List<long> GetAllCorporationIds()
        {
            return ParsedAccessLists.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
        }

        public List<long> GetAllAllianceIds()
        {
            return ParsedAccessLists.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
        }

        public override async Task Initialize()
        {
            var data = Settings.TimersModule.AccessList.ToDictionary(pair => pair.Key, pair => pair.Value.FilterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedAccessLists);
            data = Settings.TimersModule.EditList.ToDictionary(pair => pair.Key, pair => pair.Value.FilterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedEditLists);

            foreach (var id in GetAllCharacterIds())
                await APIHelper.ESIAPI.RemoveAllCharacterDataFromCache(id);

            await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.TimersModule.AccessList.Values.SelectMany(a=> a.FilterDiscordRoles).Distinct().ToList(), Category);
            await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.TimersModule.EditList.Values.SelectMany(a=> a.FilterDiscordRoles).Distinct().ToList(), Category);

            await WebPartInitialization();
        }

        private async Task WriteCorrectResponce(HttpListenerResponse response, bool isEditor, long characterId)
        {
            var baseCharId = Convert.ToBase64String(Encoding.UTF8.GetBytes(characterId.ToString()));
            var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);

            if (rChar == null)
            {
                await response.WriteContentAsync("ERROR: Probably EVE ESI is shut down at the moment. Please try again later.");
                return;
            }

            var text = File.ReadAllText(SettingsManager.FileTemplateTimersPage).Replace("{header}", LM.Get("timersTemplateHeader"))
                    .Replace("{headerContent}",
                        WebServerModule.GetHtmlResourceDefault(true) + WebServerModule.GetHtmlResourceDatetime() + WebServerModule.GetHtmlResourceConfirmation())
                    .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                    .Replace("{charId}", baseCharId)
                    .Replace("{body}", await GenerateTimersHtml(isEditor, baseCharId))
                    .Replace("{isEditorElement}", isEditor ? null : "d-none")
                    .Replace("{addNewTimerHeader}", LM.Get("timersAddHeader"))
                    .Replace("{addNewRfTimerHeader}", LM.Get("timersAddRfHeader"))
                    .Replace("{timersType}", LM.Get("timersType"))
                    .Replace("{timersStage}", LM.Get("timersStage"))
                    .Replace("{timersLocation}", LM.Get("timersLocation"))
                    .Replace("{timersOwner}", LM.Get("timersOwner"))
                    .Replace("{timersET}", LM.Get("timersET"))
                    .Replace("{timersRfET}", LM.Get("timersRfET"))
                    .Replace("{timersNotes}", LM.Get("timersNotes"))
                    .Replace("{Add}", LM.Get("Add"))
                    .Replace("{Cancel}", LM.Get("Cancel"))
                    .Replace("{timerOffensive}", LM.Get("timerOffensive"))
                    .Replace("{timerDefensive}", LM.Get("timerDefensive"))
                    .Replace("{timerHull}", LM.Get("timerHull"))
                    .Replace("{timerArmor}", LM.Get("timerArmor"))
                    .Replace("{timerShield}", LM.Get("timerShield"))
                    .Replace("{timerOther}", LM.Get("timerOther"))
                    .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                    .Replace("{LogOut}", LM.Get("LogOut"))
                    .Replace("{timerTooltipLocation}", LM.Get("timerTooltipLocation"))
                    .Replace("{timerTooltipOwner}", LM.Get("timerTooltipOwner"))
                    .Replace("{timerTooltipET}", LM.Get("timerTooltipET"))
                    .Replace("{locale}", LM.Locale)
                    .Replace("{dateformat}", Settings.TimersModule.TimeInputFormat)
                ;
            await WebServerModule.WriteResponce(text, response);
        }

        private async Task<bool[]> CheckAccess(long characterId, JsonClasses.CharacterData rChar)
        {
            var authgroups = Settings.TimersModule.AccessList;
            var accessCorps = new List<long>();
            var accessAlliance = new List<long>();
            var accessChars = new List<long>();
           // isEditor = false;
            bool skip = false;

            if (authgroups.Count == 0 || authgroups.Values.All(a => !a.FilterEntities.Any() && !a.FilterDiscordRoles.Any()))
            {
                skip = true;
            }
            else
            {
                var discordRoles = authgroups.Values.SelectMany(a => a.FilterDiscordRoles).Distinct().ToList();
                if (discordRoles.Any())
                {
                    var authUser = await SQLHelper.GetAuthUserByCharacterId(characterId);
                    if (authUser != null && authUser.DiscordId > 0)
                    {
                        if (APIHelper.DiscordAPI.GetUserRoleNames(authUser.DiscordId).Intersect(discordRoles).Any())
                            skip = true;
                    }
                }

                accessChars = ParsedAccessLists.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
                accessCorps = ParsedAccessLists.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
                accessAlliance = ParsedAccessLists.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
            }

            authgroups = Settings.TimersModule.EditList;
            var editCorps = new List<long>();
            var editAlliance = new List<long>();
            var editChars = new List<long>();
            bool skip2 = false;

            if (authgroups.Count == 0 ||  authgroups.Values.All(a => !a.FilterEntities.Any() && !a.FilterDiscordRoles.Any()))
            {
                skip2 = true;
            }
            else
            {        
                var discordRoles = authgroups.Values.SelectMany(a => a.FilterDiscordRoles).Distinct().ToList();
                if (discordRoles.Any())
                {
                    var authUser = await SQLHelper.GetAuthUserByCharacterId(characterId);
                    if (authUser != null && authUser.DiscordId > 0)
                    {
                        if (APIHelper.DiscordAPI.GetUserRoleNames(authUser.DiscordId).Intersect(discordRoles).Any())
                            skip2 = true;
                    }
                }
                editChars = ParsedEditLists.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
                editCorps = ParsedEditLists.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
                editAlliance = ParsedEditLists.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
            }

            //check for Discord admins
            if (!skip2 && Settings.TimersModule.GrantEditRolesToDiscordAdmins)
            {
                var discordId = await SQLHelper.GetAuthUserDiscordId(characterId);
                if (discordId > 0)
                {
                    var roles = string.Join(',', APIHelper.DiscordAPI.GetUserRoleNames(discordId));
                    if (!string.IsNullOrEmpty(roles))
                    {
                        var exemptRoles = Settings.Config.DiscordAdminRoles;
                        skip2 = roles.Replace("&br;", "\"").Split(',').Any(role => exemptRoles.Contains(role));
                    }
                }
            }

            if (!skip && !skip2 && !accessCorps.Contains(rChar.corporation_id) && !editCorps.Contains(rChar.corporation_id) &&
                (!rChar.alliance_id.HasValue || !(rChar.alliance_id > 0) || (!accessAlliance.Contains(
                                                                                    rChar.alliance_id
                                                                                        .Value) && !editAlliance.Contains(
                                                                                    rChar.alliance_id.Value))))
            {
                if (!editChars.Contains(characterId) && !accessChars.Contains(characterId))
                {
                    return new[] {false, false};
                }
            }

            var isEditor = skip2 || editCorps.Contains(rChar.corporation_id) || (rChar.alliance_id.HasValue && rChar.alliance_id.Value > 0 && editAlliance.Contains(rChar.alliance_id.Value))
                || editChars.Contains(characterId);

            return new [] {true, isEditor};
        }

        public async Task<string> GenerateTimersHtml(bool isEditor, string baseCharId)
        {
            var timers = await SQLHelper.SelectTimers();
            int counter = 1;
            var sb = new StringBuilder();

            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("timersType")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("timersStage")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersLocation")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersOwner")}</th>");
            sb.AppendLine($"<th scope=\"col\" class=\"fixed150\">{LM.Get("timersET")}</th>");
            sb.AppendLine($"<th scope=\"col\" class=\"fixed150\">{LM.Get("timersRemaining")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersNotes")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("timersUser")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\" class=\"{(isEditor ? null : "d-none")}\"></th");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            var timeFormat = Settings.Config.ShortTimeFormat;
            timers.OrderBy(a=> a.GetDateTime()).ToList().ForEach(timer =>
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                sb.AppendLine($"  <td>{timer.GetModeName()}</td>");
                sb.AppendLine($"  <td>{timer.GetStageName()}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerLocation)}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerOwner)}</td>");
                sb.AppendLine($"  <td>{timer.GetDateTime()?.ToString(timeFormat)}</td>");
                sb.AppendLine($"  <td>{timer.GetRemains()}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerNotes)}</td>");
                sb.AppendLine($"  <td>{HttpUtility.HtmlEncode(timer.timerChar)}</td>");
                if(isEditor)
                    sb.AppendLine($"<td><a class=\"btn btn-danger\" href=\"{WebServerModule.GetTimersURL()}?data=delete{timer.id}&id={HttpUtility.UrlEncode(baseCharId)}&state=11\" role=\"button\" data-toggle=\"confirmation\" data-title=\"{LM.Get("ConfirmDelete")}?\">X</a></td>");
                sb.AppendLine("</tr>");
            });
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private DateTime? _lastTimersCheck;

        public override async Task Run(object prm)
        {
            if(IsRunning) return;
            IsRunning = true;
            try
            {
                await ProcessTimers();
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task ProcessTimers()
        {
            try
            {
                if (_lastTimersCheck != null && (DateTime.Now - _lastTimersCheck.Value).TotalMinutes <= 1) return;
                _lastTimersCheck = DateTime.Now;

                await LogHelper.LogModule("Running timers check...", Category);
                var timers = await SQLHelper.SelectTimers();
                timers?.ForEach(async timer =>
                {
                    var channel = Settings.TimersModule.AnnounceChannel;
                    var dt = timer.GetDateTime();
                    if (dt != null && (dt.Value - DateTime.UtcNow).TotalMinutes <= 0)
                    {
                        if (channel != 0)
                            await SendNotification(timer, channel);
                        await SQLHelper.DeleteTimer(timer.id);
                        return;
                    }

                    if (channel == 0) return;

                    var announces = Settings.TimersModule.Announces.OrderByDescending(a => a).ToList();
                    if (announces.Count == 0) return;

                    //if we don;t have any lesser announce times
                    if (timer.announce != 0 && announces.Min() >= timer.announce) return;

                    if (timer.announce == 0)
                    {
                        var left = (timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes;
                        if (left <= announces.Max())
                        {
                            var value = announces.Where(a => a < left).OrderByDescending(a => a).FirstOrDefault();
                            value = value == 0 ? announces.Min() : value;
                            //announce
                            await SendNotification(timer, channel);
                            await SQLHelper.SetTimerAnnounce(timer.id, value);
                        }
                    }
                    else
                    {
                        var aList = announces.Where(a => a < timer.announce).OrderByDescending(a => a).ToList();
                        if (aList.Count == 0) return;

                        var an = aList.First();
                        if ((timer.GetDateTime().Value - DateTime.UtcNow).TotalMinutes <= an)
                        {
                            //announce
                            await SendNotification(timer, channel);
                            await SQLHelper.SetTimerAnnounce(timer.id, an);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }

        private async Task SendNotification(TimerItem timer, ulong channel)
        {
            try
            {
                var remains = timer.GetRemains();
                var stage = timer.GetStageName();
                var mode = timer.GetModeName();
                var embed = new EmbedBuilder()
                    .WithTitle(LM.Get("timerNotifyTitle", string.IsNullOrEmpty(timer.timerLocation) ? "-" : timer.timerLocation))
                    .AddField(LM.Get("timersType"), string.IsNullOrEmpty(mode) ? "-" : mode, true)
                    .AddField(LM.Get("timersStage"), string.IsNullOrEmpty(stage) ? "-" : stage, true)
                    .AddField(LM.Get("timersOwner"), string.IsNullOrEmpty(timer.timerOwner) ? "-" : timer.timerOwner, true)
                    .AddField(LM.Get("timersRemaining"), string.IsNullOrEmpty(remains) ? "-" : remains, true)
                    .AddField(LM.Get("timersNotes"), string.IsNullOrEmpty(timer.timerNotes) ? "-" : timer.timerNotes);
                if (!string.IsNullOrEmpty(Settings.Resources.ImgTimerAlert))
                    embed.WithThumbnailUrl(Settings.Resources.ImgTimerAlert);

                var ch = APIHelper.DiscordAPI.GetChannel(channel);
                if (ch == null)
                    await LogHelper.LogWarning($"Discord channel {channel} not found!", Category);
                else await APIHelper.DiscordAPI.SendMessageAsync(ch, Settings.TimersModule.DefaultMention ?? " ", embed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }

        public static async Task<string> GetUpcomingTimersString(int count = 5)
        {
            var timers = await SQLHelper.SelectTimers();
            var sb = new StringBuilder();
            if (timers.Count > 0)
            {
                for (int i = 0; i < timers.Count && i < count; i++)
                {
                    var timer = timers[i];
                    sb.Append(
                        $"[{timer.GetModeName()}][{timer.GetStageName()}] {timer.timerLocation} - {timer.GetRemains(true)} ({timer.GetDateTime().Value.ToString(SettingsManager.Settings.Config.ShortTimeFormat)} ET)\n");
                }
            }
            else
            {
                sb.Append(LM.Get("timers_none"));
            }

            return sb.ToString();
        }

    }
}
