using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Matrix.Xmpp.MessageArchiving;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public partial class HRMModule
    {
        public async Task<HRMAccessFilter> WebGetAccess(long characterId)
        {
            return await CheckAccess(characterId);
        }

        public async Task<List<WebUserItem>> WebGetUsers(UserStatusEnum userType, HRMAccessFilter filter)
        {
            IOrderedEnumerable<AuthUserEntity> list;
            switch (userType)
            {
                //case UserStatusEnum.Initial:
                //break;
                case UserStatusEnum.Awaiting:
                    list = (await SQLHelper.GetAuthUsers())
                        .Where(a => a.IsPending && IsValidUserForInteraction(filter, a))
                        .OrderBy(a => a.Data.CharacterName);
                    break;
                case UserStatusEnum.Authed:
                    list = (await SQLHelper.GetAuthUsers((int)userType))
                        .Where(a => !a.IsAltChar && IsValidUserForInteraction(filter, a))
                        .OrderBy(a => a.Data.CharacterName);
                    break;
                case UserStatusEnum.Dumped:
                    list = (await SQLHelper.GetAuthUsers((int)userType))
                        .Where(a => IsValidUserForInteraction(filter, a)).OrderBy(a => a.Data.CharacterName);
                    break;
                case UserStatusEnum.Spying:
                    list = (await SQLHelper.GetAuthUsers((int)userType))
                        .Where(a => IsValidUserForInteraction(filter, a)).OrderBy(a => a.Data.CharacterName);
                    break;
                case UserStatusEnum.Alts:
                    list = (await SQLHelper.GetAuthUsers((int)userType))
                        .Where(a => a.IsAltChar && IsValidUserForInteraction(filter, a))
                        .OrderBy(a => a.Data.CharacterName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(userType), userType, null);
            }

            var result = new List<WebUserItem>();

            foreach(var a in list)
            {

                bool invalidToken = false;
                if (a.HasToken && SettingsManager.Settings.HRMModule.ValidateTokensWhileLoading)
                {
                    var token = await APIHelper.ESIAPI.RefreshToken(a.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret, $"From HRM | Char ID: {a.CharacterId} | Char name: {a.Data.CharacterName}");
                    invalidToken = token.Result == null;
                }

                result.Add(new WebUserItem
                {
                    Id = a.CharacterId,
                    CharacterName = a.Data.CharacterName,
                    CorporationName = a.Data.CorporationName,
                    AllianceName = a.Data.AllianceName,
                    CorporationTicker = a.Data.CorporationTicker,
                    AllianceTicker = a.Data.AllianceTicker,
                    RegDate = a.CreateDate,
                    IconUrl = $"https://imageserver.eveonline.com/Character/{a.CharacterId}_64.jpg",
                    HasNoToken = !a.HasToken,
                    HasInvalidToken = invalidToken
                });
            }

            return result;
        }

        public async Task<bool> WebDeleteUser(WebUserItem order)
        {
            var sUser = await SQLHelper.GetAuthUserByCharacterId(order.Id);
            if (sUser == null)
            {
                await LogHelper.LogError($"User {order.Id} not found for delete op");
                return false;
            }

            if (Settings.HRMModule.UseDumpForMembers && !sUser.IsDumped)
            {
                sUser.SetStateDumpster();
                await LogHelper.LogInfo(
                    $"HR moving character {sUser.Data.CharacterName} to dumpster...");
                await SQLHelper.SaveAuthUser(sUser);
            }
            else
            {
                await LogHelper.LogInfo(
                    $"HR deleting character {sUser.Data.CharacterName} auth...");
                await SQLHelper.DeleteAuthDataByCharId(order.Id, true);
            }

            if (sUser.DiscordId > 0)
                await WebAuthModule.UpdateUserRoles(sUser.DiscordId,
                    Settings.WebAuthModule.ExemptDiscordRoles,
                    Settings.WebAuthModule.AuthCheckIgnoreRoles, true);
            return true;
        }

        public async Task<List<JsonClasses.CorporationHistoryEntry>> WebGenerateCorpHistory(long charId)
        {
            var history = (await APIHelper.ESIAPI.GetCharCorpHistory(Reason, charId))
                ?.OrderByDescending(a => a.record_id).ToList();
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
            if (l != null)
                l.Days = (int)(DateTime.UtcNow - l.Date).TotalDays;
            return history.Where(a=> a.Days > 0).ToList();
        }

        public async Task<List<WebMailHeader>> WebGetMailHeaders(long id, string token)
        {
            var mailHeaders = (await APIHelper.ESIAPI.GetMailHeaders(Reason, id, token, 0, null))?.Result;
            if (mailHeaders == null)
                return null;
            var list = new List<WebMailHeader>();
            foreach (var h in mailHeaders)
            {
                var from = await APIHelper.ESIAPI.GetCharacterData(Reason, h.@from);
                var rcp = await MailModule.GetRecepientNames(Reason, h.recipients, id, token);

                list.Add(new WebMailHeader
                {
                    MailId = h.mail_id,
                    FromName = from?.name ?? LM.Get("Unknown"),
                    ToName = rcp.Length > 0 ? rcp : LM.Get("Unknown"),
                    Subject = h.subject,
                    Date = h.Date
                });
            }

            return list;
        }

        public async Task<List<WebContract>> WebGetCharContracts(long id, string inspectToken)
        {
            var contracts = (await APIHelper.ESIAPI.GetCharacterContracts(Reason, id, inspectToken, null)).Result;
            if (contracts == null) return null;

            var list = new List<WebContract>();
            foreach (var entry in contracts)
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

                var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, id);
                var itemList = await ContractNotificationsModule.GetContractItemsString(Reason, entry.for_corporation, ch.corporation_id, id, entry.contract_id, inspectToken);

                list.Add(new WebContract
                {
                    Id = entry.contract_id,
                    Type = entry.type,
                    From = from,
                    To = to,
                    FromLink = $"https://zkillboard.com/{fromPlace}/{fromId}/",
                    ToLink = toId > 0 ? $"https://zkillboard.com/{toPlace}/{toId}/" : "-",
                    Status = entry.status,
                    CompleteDate = entry.DateCompleted?.ToString(Settings.Config.ShortTimeFormat) ?? LM.Get("hrmContractInProgress"),
                    Title = entry.title,
                    IncludedItems = itemList[0],
                    AskingItems = itemList[1]
                });
            }

            return list;
        }

        public async Task<List<WebContact>> WebGetCharContacts(long id, string inspectToken, long hrId)
        {
            try
            {
                var contacts = (await APIHelper.ESIAPI.GetCharacterContacts(Reason, id, inspectToken)).Result
                    .OrderByDescending(a => a.standing).ToList();
                List<JsonClasses.Contact> hrContacts = null;

                if (hrId > 0 && hrId != id)
                {
                    var hrUserInfo = await SQLHelper.GetAuthUserByCharacterId(hrId);
                    if (hrUserInfo != null && SettingsManager.HasCharContactsScope(hrUserInfo.Data.PermissionsList))
                    {
                        var hrToken = (await APIHelper.ESIAPI.RefreshToken(hrUserInfo.RefreshToken,
                                Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret
                                , $"From {Category} | Char ID: {hrUserInfo.CharacterId} | Char name: {hrUserInfo.Data.CharacterName}")
                            )?.Result;
                        if (!string.IsNullOrEmpty(hrToken))
                        {
                            hrContacts = (await APIHelper.ESIAPI.GetCharacterContacts(Reason, hrId, hrToken)).Result;
                        }
                    }
                }

                var list = new List<WebContact>();
                foreach (var entry in contacts)
                {
                    string name;
                    var color = "transparent";
                    var fontColor = "black";
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

                    var hrc = hrContacts?.FirstOrDefault(a =>
                        a.contact_type == entry.contact_type && a.contact_id == entry.contact_id)?.standing;
                    var hrStand = hrc.HasValue ? hrc.Value.ToString() : "-";
                    if (hrc.HasValue)
                    {
                        switch (hrc.Value)
                        {
                            case var s when s > 0 && s <= 5:
                                color = "#2B68C6";
                                fontColor = "white";
                                break;
                            case var s when s > 5 && s <= 10:
                                color = "#041B5D";
                                fontColor = "white";
                                break;
                            case var s when s < 0 && s >= -5:
                                color = "#BF4908";
                                fontColor = "white";
                                break;
                            case var s when s < -5 && s >= -10:
                                color = "#8D0808";
                                fontColor = "white";
                                break;
                        }
                    }

                    list.Add(new WebContact
                    {
                        Name = name,
                        Type = entry.contact_type,
                        Blocked = LM.Get(entry.is_blocked ? "webYes" : "webNo"),
                        Stand = entry.standing.ToString(),
                        HrStand = hrStand,
                        ForegroundColor = fontColor,
                        BackgroundColor = color,
                        ZkbLink = $"https://zkillboard.com/{entry.contact_type}/{entry.contact_id}/"
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        public async Task<List<WebWalletJournal>> WebGetWalletJournal(long id, string inspectToken)
        {
            var result = await APIHelper.ESIAPI.GetCharacterJournalTransactions(Reason, id, inspectToken);
            if (result == null) return null;
            var list = new List<WebWalletJournal>();
            foreach (var entry in result)
            {
                list.Add(new WebWalletJournal
                {
                    Date = entry.DateEntry,
                    Type = entry.ref_type,
                    Amount = entry.amount.ToString("N"),
                    Description = entry.description,
                    WebClass = entry.amount < 0 ? "hrmNegativeSum" : "hrmPositiveSum"
                });
            }

            return list;
        }

        public async Task<object[]> WebGetSkills(long id, string inspectToken)
        {
            var skills =
                await APIHelper.ESIAPI.GetCharSkills(Reason, id, inspectToken);

            var list = new List<WebSkillItem>();
            if (skills != null)
            {
                foreach (var skill in skills.skills)
                {
                    var t = await SQLHelper.GetTypeId(skill.skill_id);
                    if (t != null)
                    {
                        skill.DB_Name = t.name;
                        //skill.DB_Description = t.description;
                        skill.DB_Group = t.group_id;
                        var g = await SQLHelper.GetInvGroup(skill.DB_Group);
                        if (g != null)
                            skill.DB_GroupName = g.groupName;
                    }
                }

                var skillGroups = skills.skills.GroupBy(a => a.DB_Group,
                    (key, value) => new {ID = key, Value = value.ToList()});
                var skillsFinal = new List<JsonClasses.SkillEntry>();
                foreach (var skillGroup in skillGroups)
                {
                    skillsFinal.Add(new JsonClasses.SkillEntry { DB_Name = skillGroup.Value[0].DB_GroupName });
                    skillsFinal.AddRange(skillGroup.Value.OrderBy(a => a.DB_Name));
                }

                foreach (var skill in skillsFinal)
                {
                    var item = new WebSkillItem
                    {
                        Name = skill.DB_Name,
                        ValueTrained = skill.trained_skill_level,
                        ValueActive = skill.active_skill_level,
                        IsCategory = skill.skill_id==0
                    };

                    item.UpdateVisual();
                    list.Add(item);
                }
            }

            return new object[] {skills?.total_sp ?? 0, list};
        }

        public async Task<bool> WebMoveToSpies(WebUserItem order)
        {
            var charId = order.Id;
            if (charId == 0) return false;
            var user = await SQLHelper.GetAuthUserByCharacterId(charId);
            if (user == null) return false;
            user.SetStateSpying();
            await SQLHelper.SaveAuthUser(user);
            return true;
        }

        public async Task<UserStatusEnum> WebRestoreAuth(WebUserItem order)
        {
            var sUser = await SQLHelper.GetAuthUserByCharacterId(order.Id);

            if (sUser == null) return UserStatusEnum.Initial;

            //restore alt
            if (sUser.MainCharacterId > 0)
            {
                sUser.AuthState = (int)UserStatusEnum.Authed;
                await SQLHelper.SaveAuthUser(sUser);
                return UserStatusEnum.Authed;
            }

            sUser.AuthState = (int)UserStatusEnum.Awaiting;
            sUser.RegCode = Guid.NewGuid().ToString("N");
            await SQLHelper.SaveAuthUser(sUser);
            if (sUser.DiscordId > 0)
            {
                await WebAuthModule.AuthUser(null, sUser.RegCode, sUser.DiscordId);
                return UserStatusEnum.Authed;
            }
            else return UserStatusEnum.Awaiting;
        }
    }

    [Serializable]
    public class WebWalletJournal
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Amount { get; set; }
        public string Description { get; set; }

        public string WebClass;
    }

    [Serializable]
    public class WebWalletTrans
    {

    }

    [Serializable]
    public class WebContact
    {
        public string Name;
        public string HrStand;
        public string ForegroundColor;
        public string BackgroundColor;
        public string Type;
        public string Blocked;
        public string Stand;
        public string ZkbLink;
    }

    [Serializable]
    public class WebContract
    {
        public long Id;
        public string Type;
        public string FromLink;
        public string ToLink;
        public string Status;
        public string CompleteDate;
        public string Title;
        public string IncludedItems;
        public string AskingItems;
        public string From;
        public string To;
    }

    [Serializable]
    public class WebMailHeader
    {
        public string Subject;
        public string ToName;
        public string FromName;
        public long MailId;
        public DateTime Date;
    }
}
