using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public partial class HRMModule
    {
        private static bool IsNoRedirect(string data)
        {
            return data.StartsWith("mail");
        }

        private async Task<int> GetMailPagesCount(string token, int inspectCharId)
        {
            var mailHeaders = await APIHelper.ESIAPI.GetMailHeaders(Reason, inspectCharId.ToString(), token, 0);
            return  (mailHeaders?.Count ?? 0) / Settings.HRMModule.TableEntriesPerPage;        
        }

        private async Task<int> GetCharJournalPagesCount(string token, int inspectCharId)
        {
            var entries = await APIHelper.ESIAPI.GetCharacterWalletJournal(Reason, inspectCharId, token);
            return  (entries?.Count ?? 0) / Settings.HRMModule.TableEntriesPerPage;        
        }

        private int GetCharJournalPagesCount()
        {
            return 3;
        }

        private async Task<int> GetCharContractsPagesCount(string token, int inspectCharId)
        {
            var entries = await APIHelper.ESIAPI.GetCharacterContracts(Reason, inspectCharId, token);
            return  (entries?.Count ?? 0) / Settings.HRMModule.TableEntriesPerPage; 
        }
        private async Task<int> GetCharContactsPagesCount(string token, int inspectCharId)
        {
            var entries = await APIHelper.ESIAPI.GetCharacterContacts(Reason, inspectCharId, token);
            return  (entries?.Count ?? 0) / Settings.HRMModule.TableEntriesPerPage; 
        }

        private async Task<int> GetCharSkillsPagesCount(string token, int inspectCharId)
        {
            var entries = await APIHelper.ESIAPI.GetCharSkills(Reason, inspectCharId, token);
            if (entries == null) return 0;
            await entries.PopulateNames();
            var groupCount = entries.skills.Select(a => a.DB_Group).Distinct().Count();

            return  (entries.skills.Count + groupCount) / (Settings.HRMModule.TableSkillEntriesPerPage * 3); 
        }


        private async Task<int> GetCharTransactPagesCount(string token, int inspectCharId)
        {
            var entries = await APIHelper.ESIAPI.GetCharacterWalletTransactions(Reason, inspectCharId, token);
            return  (entries?.Count ?? 0) / Settings.HRMModule.TableEntriesPerPage;        
        }

        private async Task<string> GenerateMailHtml(string token, int inspectCharId, string authCode, int page)
        {
            var mailHeaders = await APIHelper.ESIAPI.GetMailHeaders(Reason, inspectCharId.ToString(), token, 0);
            //var totalCount = mailHeaders.Count;
            var startIndex = (page-1) * Settings.HRMModule.TableEntriesPerPage;
            mailHeaders = mailHeaders.GetRange(startIndex, mailHeaders.Count > startIndex+Settings.HRMModule.TableEntriesPerPage ? Settings.HRMModule.TableEntriesPerPage : (mailHeaders.Count-startIndex));

            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("mailSubjectHeader")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("mailFromHeader")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("mailDateHeader")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex+ 1;
            foreach (var entry in mailHeaders)
            {
                var from = await APIHelper.ESIAPI.GetCharacterData(Reason, entry.@from);
                var mailBodyUrl = WebServerModule.GetHRM_AjaxMailURL(entry.mail_id, inspectCharId, authCode);

                sb.AppendLine("<tr>");
                sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                sb.AppendLine($"  <td><a href=\"#\" onclick=\"openMailDialog('{mailBodyUrl}')\">{entry.subject}</td>");
                sb.AppendLine($"  <td>{from?.name ?? LM.Get("Unknown")}</td>");
                sb.AppendLine($"  <td>{entry.Date.ToShortDateString()}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateCorpHistory(long charId)
        {
            var history = (await APIHelper.ESIAPI.GetCharCorpHistory(Reason, charId))?.OrderByDescending(a=> a.record_id).ToList();
            if (history == null || history.Count == 0) return null;

            JsonClasses.CorporationHistoryEntry last = null;
            foreach (var entry in history)
            {
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, entry.corporation_id);
                entry.CorpName = corp.name;
                entry.IsNpcCorp = corp.creator_id == 1;
                if (last != null)
                {
                    entry.Days = (int)(last.Date - entry.Date).TotalDays;
                }

                entry.CorpTicker = corp.ticker;
                last = entry;
            }

            var l = history.FirstOrDefault();
            if(l != null)
                l.Days = (int)(DateTime.UtcNow - l.Date).TotalDays;

            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmCVCorpName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmCVJoined")}</th>");
            sb.AppendLine($"<th scope=\"col\">{LM.Get("hrmCVDays")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = 1;
            foreach (var entry in history)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                sb.AppendLine($"  <td><a href=\"https://zkillboard.com/corporation/{entry.corporation_id}\">{entry.CorpName}</a>[{entry.CorpTicker}]{(entry.IsNpcCorp ? " (npc)" : null)}{(entry.is_deleted ? " (closed)" : null)}</td>");
                sb.AppendLine($"  <td>{entry.Date.ToShortDateString()}</td>");
                sb.AppendLine($"  <td>{entry.Days}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateMembersListHtml(string authCode)
        {
            var list = await SQLHelper.UserTokensGetAllEntries();
            var sb = new StringBuilder();
            foreach (var item in list.Where(a=> a.AuthState == 2))
            {
                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, item.CharacterId, true);
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id);
                var ally = corp.alliance_id.HasValue ?  await APIHelper.ESIAPI.GetAllianceData(Reason, corp.alliance_id) : null;
                var charUrl = WebServerModule.GetHRMInspectURL(item.CharacterId, authCode);
                int counter = 1;
                sb.Append($"<div class=\"row-fluid\" style=\"margin-top: 5px;\">");
                sb.Append($"<img src=\"https://imageserver.eveonline.com/Character/{item.CharacterId}_64.jpg\" style=\"width:64;height:64;\"/>");
                sb.Append($@"<a class=""btn btn-outline-info btn-block"" href=""{charUrl}"">");
                sb.Append($@"<div class=""container""><div class=""row""><b>{item.CharacterName}</b></div><div class=""row"">{corp.name} [{corp.ticker}]{(ally != null ? $" - {ally.name}[{ally.ticker}]" : null)}</div></div>");
                sb.Append(@"</a>");
                sb.Append($"</div>");
                counter++;
            }
            
            return sb.ToString();
        }

        private async Task<string> GenerateAwaitingListHtml(string authCode)
        {
            var list = await SQLHelper.UserTokensGetAllEntries();
            var sb = new StringBuilder();
            foreach (var item in list.Where(a=> a.AuthState != 2))
            {
                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, item.CharacterId, true);
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id);
                var ally = corp.alliance_id.HasValue ?  await APIHelper.ESIAPI.GetAllianceData(Reason, corp.alliance_id) : null;
                var charUrl = WebServerModule.GetHRMInspectURL(item.CharacterId, authCode);
                int counter = 1;
                sb.Append($"<div class=\"row-fluid\" style=\"margin-top: 5px;\">");
                sb.Append($"<img src=\"https://imageserver.eveonline.com/Character/{item.CharacterId}_64.jpg\" style=\"width:64;height:64;\"/>");
                sb.Append($@"<a class=""btn btn-outline-info btn-block"" href=""{charUrl}"">");
                sb.Append($@"<div class=""container""><div class=""row""><b>{item.CharacterName}</b></div><div class=""row"">{corp.name} [{corp.ticker}]{(ally != null ? $" - {ally.name}[{ally.ticker}]" : null)}</div></div>");
                sb.Append(@"</a>");
                sb.Append($"</div>");
                counter++;
            }
            
            return sb.ToString();
        }

        private async Task<string> GenerateTransactionsHtml(string token, long inspectCharId, int page)
        {
            var items = await APIHelper.ESIAPI.GetCharacterWalletTransactions(Reason, inspectCharId, token);
            //var totalCount = mailHeaders.Count;
            var startIndex = (page-1) * Settings.HRMModule.TableEntriesPerPage;
            items = items.GetRange(startIndex, items.Count > startIndex+Settings.HRMModule.TableEntriesPerPage ? Settings.HRMModule.TableEntriesPerPage : (items.Count-startIndex));

            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmTransDateHeader")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmTransTypeHeader")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmTransCreditHeader")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmTransClientHeader")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmTransWhereHeader")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex+ 1;
            foreach (var entry in items)
            {
                try
                {
                    var fromChar = await APIHelper.ESIAPI.GetCharacterData(Reason, entry.client_id);
                    var from = fromChar?.name ?? (await APIHelper.ESIAPI.GetCorporationData(Reason, entry.client_id))?.name;
                    var urlSection = fromChar == null ? "corporation" : "character";
                    var bgClass = entry.is_buy ? "bgNegativeSum" :  "bgPositiveSum";
                    var foreColor = entry.is_buy ? "fgNegativeSum" : "fgPositiveSum";
                    var type = await APIHelper.ESIAPI.GetTypeId(Reason, entry.type_id);
                    var amount = entry.quantity * entry.unit_price * (entry.is_buy ? -1 : 1);
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                    sb.AppendLine($"  <td>{entry.DateEntry.ToString(Settings.Config.ShortTimeFormat)}</td>");
                    sb.AppendLine($"  <td>{type?.name}</td>");
                    sb.AppendLine($"  <td class=\"{bgClass}\"><font class=\"{foreColor}\">{amount:N}</font></td>");
                    sb.AppendLine($"  <td><a href=\"https://zkillboard.com/{urlSection}/{entry.client_id}/\">{from}</a></td>");
                    sb.AppendLine($"  <td>{entry.location_id}</td>");
                    sb.AppendLine("</tr>");
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("", ex, Category);
                }
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateContractsHtml(string token, long inspectCharId, int page)
        {
            var items = (await APIHelper.ESIAPI.GetCharacterContracts(Reason, inspectCharId, token)).OrderByDescending(a=> a.contract_id).ToList();
            var startIndex = (page-1) * Settings.HRMModule.TableEntriesPerPage;
            items = items.GetRange(startIndex, items.Count > startIndex+Settings.HRMModule.TableEntriesPerPage ? Settings.HRMModule.TableEntriesPerPage : (items.Count-startIndex));
            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractType")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractFrom")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractTo")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractStatus")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractDate")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractInfo")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractItems")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex + 1;
            foreach (var entry in items)
            {
                try
                {
                    var fromPlace = entry.issuer_id != 0 ? "character" : "corporation";
                    var toPlace = !entry.for_corporation ? "character" : "corporation";
                    var fromId = entry.issuer_id != 0 ? entry.issuer_id : entry.issuer_corporation_id;
                    var toId = entry.acceptor_id;
                    var from = entry.issuer_id != 0
                        ? (await APIHelper.ESIAPI.GetCharacterData(Reason, entry.issuer_id))?.name
                        : (await APIHelper.ESIAPI.GetCorporationData(Reason, entry.issuer_corporation_id))?.name;
                    var to = entry.for_corporation
                        ? (await APIHelper.ESIAPI.GetCorporationData(Reason, entry.acceptor_id))?.name
                        : (await APIHelper.ESIAPI.GetCharacterData(Reason, entry.acceptor_id))?.name;

                    var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, inspectCharId);
                    var itemList = await ContractNotificationsModule.GetContarctItemsString(Reason, entry.for_corporation, ch.corporation_id ,inspectCharId , entry.contract_id, token);

                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                    sb.AppendLine($"  <td>Contract</td>");
                    sb.AppendLine($"  <td>{entry.type}</td>");
                    sb.AppendLine($"  <td><a href=\"https://zkillboard.com/{fromPlace}/{fromId}/\">{from}</a></td>");
                    sb.AppendLine(toId > 0 ? $"  <td><a href=\"https://zkillboard.com/{toPlace}/{toId}/\">{to}</a></td>" : "  <td>-</td>");
                    sb.AppendLine($"  <td>{entry.status}</td>");
                    sb.AppendLine($"  <td>{entry.DateCompleted?.ToString(Settings.Config.ShortTimeFormat) ?? LM.Get("hrmContractInProgress")}</td>");
                    sb.AppendLine($"  <td>{entry.title}</td>");
                    if(!string.IsNullOrEmpty(itemList[0]))
                        sb.AppendLine($"  <td>{LM.Get("contractMsgIncludedItems")}<img src=\"https://github.com/panthernet/ThunderED/blob/master/ThunderED/Content/Icons/itemIconSmall.png?raw=true\" alt=\"\" title=\"{itemList[0]}\"/></td>");
                    if(!string.IsNullOrEmpty(itemList[1]))
                        sb.AppendLine($"  <td>{LM.Get("contractMsgAskingItems")}<img src=\"https://github.com/panthernet/ThunderED/blob/master/ThunderED/Content/Icons/itemIconSmall.png?raw=true\" alt=\"\" title=\"{itemList[1]}\"/></td>");
                    if(string.IsNullOrEmpty(itemList[0]) && string.IsNullOrEmpty(itemList[1]))
                    sb.AppendLine($"  <td>-</td>");
                    sb.AppendLine("</tr>");
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("", ex, Category);
                }
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateSkillsHtml(string token, int inspectCharId, int page)
        {
            var data = await APIHelper.ESIAPI.GetCharSkills(Reason, inspectCharId, token);
            if (data == null) return null;
            await data.PopulateNames();
            var skillGroups = data.skills.GroupBy(a => a.DB_Group, (key, value) => new { ID = key, Value = value.ToList()});
            var skills = new List<JsonClasses.SkillEntry>();
            foreach (var skillGroup in skillGroups)
            {
                skills.Add(new JsonClasses.SkillEntry { DB_Name = skillGroup.Value[0].DB_GroupName});
                skills.AddRange(skillGroup.Value.OrderBy(a=> a.DB_Name));
            }

            var max = Settings.HRMModule.TableSkillEntriesPerPage * 3;
            var startIndex = (page - 1) * max;
            skills = skills.GetRange(startIndex, skills.Count > startIndex+max ? max : (skills.Count-startIndex));
            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillLevel")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillLevel")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmSkillLevel")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex + 1;

            var rowEntryCounter = 1;
            var maxRowEntries = 3;
            for (var index = 0; index < skills.Count; index++)
            {
                var entry = skills[index];

                var bgcolor = GetSkillCellColor(entry.trained_skill_level, string.IsNullOrEmpty(entry.DB_GroupName));
                if (rowEntryCounter == 1)
                {
                    sb.AppendLine("<tr>");
                }

                //group
                if (string.IsNullOrEmpty(entry.DB_GroupName))
                {
                    if (rowEntryCounter > 1)
                    {
                        for (int i = rowEntryCounter; i < maxRowEntries; i++)
                        {
                            sb.AppendLine("  <td></td>");
                            sb.AppendLine("  <td></td>");
                        }

                        sb.AppendLine("</tr>");
                        sb.AppendLine("<tr>");
                        rowEntryCounter = 1;
                    }

                    sb.AppendLine($"  <th scope=\"row\"></th>");
                    sb.AppendLine($"  <td><b>{entry.DB_Name}</b></td>");
                    sb.AppendLine($"  <td></td>");
                    sb.AppendLine($"  <td></td>");
                    sb.AppendLine($"  <td></td>");
                    sb.AppendLine($"  <td></td>");
                    sb.AppendLine($"  <td></td>");
                    sb.AppendLine("</tr>");
                    continue;
                }
                //item
                if(rowEntryCounter == 1)
                    sb.AppendLine($"  <th bgcolor=\"{bgcolor}\" scope=\"row\">{counter++}</th>");

                sb.AppendLine($"  <td bgcolor=\"{bgcolor}\">{entry.DB_Name}</td>");
                sb.AppendLine($"  <td bgcolor=\"{bgcolor}\">{entry.trained_skill_level}</td>");

                rowEntryCounter++;
                if (rowEntryCounter > maxRowEntries)
                {
                    sb.AppendLine("</tr>");
                    rowEntryCounter = 1;
                }

            }


            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private string GetSkillCellColor(int entryTrainedSkillLevel, bool isCategory)
        {
            if (isCategory) entryTrainedSkillLevel = -1;
            var bgcolor = "white";
            switch (entryTrainedSkillLevel)
            {
                case -1:
                    return "gray";
                case 0:
                    return bgcolor;
                case var a when a < 5 && a > 2:
                    bgcolor = "yellow";
                    break;
                case var a when a <= 2:
                    bgcolor = "#C0C0C0";
                    break;
                case 5:
                    bgcolor = "green";
                    break;
            }

            return bgcolor;
        }


        private async Task<string> GenerateContactsHtml(string token, int inspectCharId, int page)
        {
            var items = (await APIHelper.ESIAPI.GetCharacterContacts(Reason, inspectCharId, token)).OrderByDescending(a=> a.standing).ToList();
            var startIndex = (page-1) * Settings.HRMModule.TableEntriesPerPage;
            items = items.GetRange(startIndex, items.Count > startIndex+Settings.HRMModule.TableEntriesPerPage ? Settings.HRMModule.TableEntriesPerPage : (items.Count-startIndex));
            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th scope=\"col-md-auto\">#</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContactName")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContactType")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractBlocked")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmContractStand")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex + 1;
            foreach (var entry in items)
            {
                try
                {
                    string name;
                    switch (entry.contact_type)
                    {
                        case "character":
                            var c = await APIHelper.ESIAPI.GetCharacterData(Reason, entry.contact_id);
                            name = c?.name;
                            break;
                        case "corporation":
                            var co = await APIHelper.ESIAPI.GetCorporationData(Reason, entry.contact_id);
                            name = co?.name;
                            break;
                        case "alliance":
                            var al = await APIHelper.ESIAPI.GetAllianceData(Reason, entry.contact_id);
                            name = al?.name;
                            break;
                        case "faction":
                            var f = await APIHelper.ESIAPI.GetFactionData(Reason, entry.contact_id);
                            name = f?.name;
                            break;
                        default:
                            name = null;
                            break;
                    }


                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                    sb.AppendLine($"  <td><a href=\"https://zkillboard.com/{entry.contact_type}/{entry.contact_id}/\">{name}</a></td>");
                    sb.AppendLine($"  <td>{entry.contact_type}</td>");
                    sb.AppendLine($"  <td>{entry.is_blocked}</td>");
                    sb.AppendLine($"  <td>{entry.standing}</td>");
                    sb.AppendLine("</tr>");
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("", ex, Category);
                }
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateJournalHtml(string token, int inspectCharId, int page)
        {
            var items = await APIHelper.ESIAPI.GetCharacterJournalTransactions(Reason, inspectCharId, token);
            //var totalCount = mailHeaders.Count;
            var startIndex = (page-1) * Settings.HRMModule.TableEntriesPerPage;
            items = items.GetRange(startIndex, items.Count > startIndex+Settings.HRMModule.TableEntriesPerPage ? Settings.HRMModule.TableEntriesPerPage : (items.Count-startIndex));

            var sb = new StringBuilder();
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine($"<th>#</th>");
            sb.AppendLine($"<th style=\"width: 15%\">{LM.Get("hrmJournalDate")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmJournalType")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmJournalAmount")}</th>");
            sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmJournalDescription")}</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");
            var counter = startIndex+ 1;
            foreach (var entry in items)
            {
                try
                {
                    var bgClass = entry.amount < 0 ? "bgNegativeSum" :  "bgPositiveSum";
                    var foreColor = entry.amount <0? "fgNegativeSum" : "fgPositiveSum";
                    sb.AppendLine($"<tr>");
                    sb.AppendLine($"  <th scope=\"row\">{counter++}</th>");
                    sb.AppendLine($"  <td>{entry.DateEntry.ToString(Settings.Config.ShortTimeFormat)}</td>");
                    sb.AppendLine($"  <td>{entry.ref_type}</td>");
                    sb.AppendLine($"  <td class=\"{bgClass}\"><font class=\"{foreColor}\">{entry.amount:N}</font></td>");
                    sb.AppendLine($"  <td>{entry.description}</td>");
                    sb.AppendLine("</tr>");
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("", ex, Category);
                }
            }
            sb.AppendLine("</tbody>");
            return sb.ToString();
        }

        private async Task<string> GenerateLysHtml(string token, int inspectCharId, int page)
        {
            var list = await APIHelper.ESIAPI.GetCharYearlyStats(Reason, inspectCharId, token);
            var item = list.FirstOrDefault();
            if (item == null) return LM.Get("hrmCharNoHistory");
            var sb = new StringBuilder();
            switch (page)
            {
                case 1:
                {
                    sb.AppendLine("<thead>");
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysEntry")}</th>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysValue")}</th>");
                    sb.AppendLine("</tr>");
                    sb.AppendLine("</thead>");
                    sb.AppendLine("<tbody>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategory")}</td><td><b>{LM.Get("hrmLysCategoryChar")}</b></td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharActivity")}</td><td>{item.character?.days_of_activity ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharSessions")}</td><td>{item.character?.sessions_started ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharIskIn")}</td><td>{item.isk?.@in ?? 0:N}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharIskOut")}</td><td>{item.isk?.@out ?? 0:N}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharAccContr")}</td><td>{item.market?.accept_contracts_item_exchange ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharBuyOrders")}</td><td>{item.market?.buy_orders_placed ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharCouriers")}</td><td>{item.market?.create_contracts_courier ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharCrContr")}</td><td>{item.market?.create_contracts_item_exchange ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCharSellOrders")}</td><td>{item.market?.sell_orders_placed ?? 0}</td></tr>");
                    sb.AppendLine("</tbody>");
                }
                    break;
                case 2:
                {
                    sb.AppendLine("<thead>");
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysEntry")}</th>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysValue")}</th>");
                    sb.AppendLine("</tr>");
                    sb.AppendLine("</thead>");
                    sb.AppendLine("<tbody>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategory")}</td><td><b>{LM.Get("hrmLysCategoryCombat")}</b></td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatCriminal")}</td><td>{item.combat?.criminal_flag_set ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatDeaths")}</td><td>{item.combat?.deaths_high_sec ?? 0}/{item.combat?.deaths_low_sec ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatDuels")}</td><td>{item.combat?.duel_requested ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatKillAss")}</td><td>{item.combat?.kills_assists ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatKills")}</td><td>{item.combat?.kills_high_sec ?? 0}/{item.combat?.kills_low_sec ?? 0}/{item.combat?.kills_null_sec ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatPvpFlags")}</td><td>{item.combat?.pvp_flag_set ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatDictor")}</td><td>{item.module?.activations_interdiction_sphere_launcher ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatMissions")}</td><td>{item.pve?.missions_succeeded ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategoryCombatAgents")}</td><td>{item.pve?.dungeons_completed_distribution ?? 0}</td></tr>");
                    sb.AppendLine("</tbody>");
                }
                    break;
                case 3:
                {
                    var totalMsg = item.social == null ? 0 : (item.social.chat_messages_alliance + item.social.chat_messages_corporation + item.social.chat_messages_fleet +
                                   item.social.chat_messages_solarsystem + item.social.chat_messages_warfaction);
                    var totalAu = item.travel == null ? 0 : (item.travel.distance_warped_high_sec + item.travel.distance_warped_low_sec + item.travel.distance_warped_null_sec +
                                  item.travel.distance_warped_wormhole);

                    var percHigh = item.travel == null ? 0 : Math.Round(100 / (totalAu / (double)item.travel.distance_warped_high_sec));
                    var percLow = item.travel == null ? 0 : Math.Round(100 / (totalAu / (double)item.travel.distance_warped_low_sec));
                    var percNull = item.travel == null ? 0 : Math.Round(100 / (totalAu / (double)item.travel.distance_warped_null_sec));
                    var percWh = item.travel == null ? 0 : Math.Round(100 / (totalAu /(double)item.travel.distance_warped_wormhole));

                    sb.AppendLine("<thead>");
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysEntry")}</th>");
                    sb.AppendLine($"<th scope=\"col-md-auto\">{LM.Get("hrmLysValue")}</th>");
                    sb.AppendLine("</tr>");
                    sb.AppendLine("</thead>");
                    sb.AppendLine("<tbody>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategory")}</td><td><b>{LM.Get("hrmLysCategorySocial")}</b></td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategorySocialAddedAs")}</td><td>{item.social?.added_as_contact_high ?? 0}/{item.social?.added_as_contact_good ?? 0}/{item.social?.added_as_contact_neutral ?? 0}/{item.social?.added_as_contact_bad ?? 0}/{item.social?.added_as_contact_horrible ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategorySocialTotalMsg")}</td><td>{totalMsg}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategorySocialDirectTrades")}</td><td>{item.social?.direct_trades ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategorySocialFleetJoins")}</td><td>{item.social?.fleet_joins ?? 0}</td></tr>");
                    sb.AppendLine($"<tr><td>{LM.Get("hrmLysCategorySocialSpaceTime")}</td><td>{percHigh}%/{percLow}%/{percNull}%/{percWh}%</td></tr>");
                    sb.AppendLine("</tbody>");
                }
                    break;
            }

            return sb.ToString();
        }

    }
}
