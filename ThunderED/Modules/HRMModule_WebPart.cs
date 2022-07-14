using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public partial class HRMModule
    {
        public async Task<HRMAccessFilter> WebGetAccess(long characterId)
        {
            return await CheckAccess(characterId);
        }

        public async Task<List<WebUserItem>> WebGetUsers(List<ThdAuthUser> users, HRMAccessFilter filter)
        {
            var result = new List<WebUserItem>();
            foreach (var a in users)
            {
                //await a.UpdateData();

                result.Add(new WebUserItem
                {
                    Id = a.CharacterId,
                    CharacterName = a.CharacterName,
                    CorporationName = a.DataView.CorporationName,
                    AllianceName = a.DataView.AllianceName,
                    CorporationTicker = a.DataView.CorporationTicker,
                    AllianceTicker = a.DataView.AllianceTicker,
                    RegDate = a.CreateDate ?? DateTime.MinValue,
                    IconUrl = $"https://imageserver.eveonline.com/Character/{a.CharacterId}_64.jpg",
                    HasNoToken = !a.HasToken,
                    HasInvalidToken = false
                });
            }

            await Task.CompletedTask;
            return result;
        }

        public async Task<bool> WebDeleteUser(WebUserItem order)
        {
            try
            {
                var sUser = await DbHelper.GetAuthUser(order.Id);
                if (sUser == null)
                {
                    await LogHelper.LogError($"User {order.Id} not found for delete op");
                    return false;
                }

                if (Settings.HRMModule.UseDumpForMembers && sUser.AuthState != (int)UserStatusEnum.Dumped)
                {
                    sUser.SetStateDumpster();
                    await LogHelper.LogInfo(
                        $"HR moving character {sUser.CharacterName} to dumpster...");
                    await DbHelper.SaveAuthUser(sUser);
                }
                else
                {
                    await LogHelper.LogInfo(
                        $"HR deleting character {sUser.CharacterName} auth...");
                    await DbHelper.DeleteAuthUser(order.Id, true);
                }

                if (sUser.DiscordId > 0)
                    await WebAuthModule.UpdateUserRoles(sUser.DiscordId ?? 0,
                        Settings.WebAuthModule.ExemptDiscordRoles.ToList(),
                        Settings.WebAuthModule.AuthCheckIgnoreRoles, true);
                return true;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return false;
            }
        }

        public async Task<List<JsonClasses.CorporationHistoryEntry>> WebGenerateCorpHistory(long charId)
        {
            try
            {
                var history = (await APIHelper.ESIAPI.GetCharCorpHistory(Reason, charId))
                    ?.OrderByDescending(a => a.record_id).ToList();
                if (history == null || history.Count == 0) return null;

                JsonClasses.CorporationHistoryEntry last = null;
                foreach (var entry in history)
                {
                    var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, entry.corporation_id);
                    if (corp == null) continue;
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
                return history.Where(a => a.Days > 0).ToList();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        public async Task<List<WebMailHeader>> WebGetMailHeaders(long id, string token)
        {
            try
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
                        FromLink = from == null ? null : $"https://zkillboard.com/character/{from.character_id}",
                        ToName = rcp.Length > 0 ? rcp : LM.Get("Unknown"),
                        Subject = h.subject,
                        Date = h.Date
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

        public async Task<List<WebContract>> WebGetCharContracts(long id, string inspectToken)
        {
            try
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
                    var itemList = await ContractNotificationsModule.GetContractItemsString(Reason,
                        entry.for_corporation, ch.corporation_id, id, entry.contract_id, inspectToken);

                    list.Add(new WebContract
                    {
                        Id = entry.contract_id,
                        Type = entry.type,
                        From = from,
                        To = to,
                        FromLink = $"https://zkillboard.com/{fromPlace}/{fromId}/",
                        ToLink = toId > 0 ? $"https://zkillboard.com/{toPlace}/{toId}/" : "-",
                        Status = entry.status,
                        CompleteDate =
                            entry.DateCompleted?.ToString(Settings.Config.ShortTimeFormat) ??
                            LM.Get("hrmContractInProgress"),
                        Title = entry.title,
                        IncludedItems = itemList[0],
                        AskingItems = itemList[1]
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

        public async Task<List<WebContact>> WebGetCharContacts(long id, string inspectToken, long hrId)
        {
            try
            {
                var contacts = (await APIHelper.ESIAPI.GetCharacterContacts(Reason, id, inspectToken)).Result
                    .OrderByDescending(a => a.standing).ToList();
                List<JsonClasses.Contact> hrContacts = null;

                if (hrId > 0 && hrId != id)
                {
                    var hrUserInfo = await DbHelper.GetAuthUser(hrId, true);
                    if (hrUserInfo != null && SettingsManager.HasCharContactsScope(hrUserInfo.GetGeneralToken()?.GetSplitScopes()))
                    {
                        var hrToken = (await APIHelper.ESIAPI.GetAccessTokenWithScopes(hrUserInfo.GetGeneralToken(), new ESIScope().AddCharContacts(),
                                $"From {Category} | Char ID: {hrUserInfo.CharacterId} | Char name: {hrUserInfo.CharacterName}"))
                            ?.Result;
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
            try
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
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        public async Task<List<WebWalletTrans>> WebGetWalletTrans(long id, string inspectToken)
        {
            try
            {
                var result = await APIHelper.ESIAPI.GetCharacterWalletTransactions(Reason, id, inspectToken);
                if (result == null) return null;
                var list = new List<WebWalletTrans>();
                foreach (var entry in result)
                {
                    var type = await APIHelper.ESIAPI.GetTypeId(Reason, entry.type_id);
                    var amount = entry.quantity * entry.unit_price * (entry.is_buy ? -1 : 1);
                    var fromChar = await APIHelper.ESIAPI.GetCharacterData(Reason, entry.client_id);
                    var from = fromChar?.name ??
                               (await APIHelper.ESIAPI.GetCorporationData(Reason, entry.client_id))?.name;
                    var urlSection = fromChar == null ? "corporation" : "character";

                    list.Add(new WebWalletTrans
                    {
                        Date = entry.DateEntry,
                        Type = type?.Name,
                        Credit = amount,
                        Client = from,
                        ClientZkbLink = $"https://zkillboard.com/{urlSection}/{entry.client_id}/",
                        Where = entry.location_id.ToString()
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

        public async Task<object[]> WebGetSkills(long id, string inspectToken)
        {
            try
            {
                var skills =
                    await APIHelper.ESIAPI.GetCharSkills(Reason, id, inspectToken);

                var list = new List<WebSkillItem>();
                if (skills != null)
                {
                    foreach (var skill in skills.skills)
                    {
                        var t = await DbHelper.GetTypeId(skill.skill_id);
                        if (t != null)
                        {
                            skill.DB_Name = t.Name;
                            //skill.DB_Description = t.description;
                            skill.DB_Group = t.GroupId;
                            var g = await DbHelper.GetInvGroup(skill.DB_Group);
                            if (g != null)
                                skill.DB_GroupName = g.GroupName;
                        }
                    }

                    var skillGroups = skills.skills.GroupBy(a => a.DB_Group,
                        (key, value) => new {ID = key, Value = value.ToList()});
                    var skillsFinal = new List<JsonClasses.SkillEntry>();
                    foreach (var skillGroup in skillGroups)
                    {
                        skillsFinal.Add(new JsonClasses.SkillEntry {DB_Name = skillGroup.Value[0].DB_GroupName});
                        skillsFinal.AddRange(skillGroup.Value.OrderBy(a => a.DB_Name));
                    }

                    foreach (var skill in skillsFinal)
                    {
                        var item = new WebSkillItem
                        {
                            Name = skill.DB_Name,
                            ValueTrained = skill.trained_skill_level,
                            ValueActive = skill.active_skill_level,
                            IsCategory = skill.skill_id == 0
                        };

                        item.UpdateVisual();
                        list.Add(item);
                    }
                }

                return new object[] {skills?.total_sp ?? 0, list};
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
        }

        public async Task<bool> WebMoveToSpies(WebUserItem order)
        {
            var charId = order.Id;
            if (charId == 0) return false;
            var user = await DbHelper.GetAuthUser(charId);
            if (user == null) return false;
            user.SetStateSpying();
            await DbHelper.SaveAuthUser(user);
            return true;
        }

        public async Task<UserStatusEnum> WebRestoreAuth(WebUserItem order)
        {
            var sUser = await DbHelper.GetAuthUser(order.Id);

            if (sUser == null) return UserStatusEnum.Initial;

            //restore alt
            if (sUser.MainCharacterId > 0)
            {
                sUser.SetStateAuthed();
                await DbHelper.SaveAuthUser(sUser);
                return UserStatusEnum.Authed;
            }

            sUser.SetStateAwaiting();
            sUser.RegCode = Guid.NewGuid().ToString("N");
            await DbHelper.SaveAuthUser(sUser);
            if (sUser.DiscordId > 0)
            {
                await WebAuthModule.AuthUser(null, sUser.RegCode, sUser.DiscordId ?? 0, SettingsManager.Settings.Config.DiscordGuildId);
                return UserStatusEnum.Authed;
            }
            return UserStatusEnum.Awaiting;
        }

        public async Task<List<WebAssetData>> WebGetCharAssetsList(long characterId, string inspectToken)
        {
            try
            {
                var assets = await APIHelper.ESIAPI.GetCharacterAssets(characterId, inspectToken);
                if (assets.Result == null) return null;
                var result = new List<WebAssetData>();

                foreach (var asset in assets.Result)
                {
                    var item = new WebAssetData
                    {
                        IsBlueprintCopy = asset.is_blueprint_copy,
                        ItemTypeId = asset.type_id ?? 0,
                        LocationId = asset.location_id,
                        Quantity = asset.quantity,
                        ItemTypeName = (await APIHelper.ESIAPI.GetTypeId(Reason, asset.type_id ?? 0))?.Name ??
                                       "Unknown"
                    };
                    switch (asset.location_type)
                    {
                        case AssetLocationType.station:
                            item.LocationName =
                                (await APIHelper.ESIAPI.GetStationData(Reason, asset.location_id, inspectToken))?.name;
                            break;
                        case AssetLocationType.solar_system:
                            item.LocationName = (await APIHelper.ESIAPI.GetSystemData(Reason, asset.location_id))?.SolarSystemName;
                            break;
                        case AssetLocationType.item:
                            item.LocationName = (await APIHelper.ESIAPI.GetTypeId(Reason, asset.location_id))?.Name;
                            break;
                        case AssetLocationType.other:
                            item.LocationName = "Unknown";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
                return null;
            }
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
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public double Credit { get; set; }
        public string Client { get; set; }
        public string Where { get; set; }
        public string ClientZkbLink { get; set; }
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

        public string AllItems => $"{IncludedItems},{AskingItems}";
    }

    [Serializable]
    public class WebMailHeader
    {
        public string Subject;
        public string ToName;
        public string FromName;
        public long MailId;
        public DateTime Date;
        public string FromLink { get; set; }
    }
}
