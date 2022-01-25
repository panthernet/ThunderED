using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public partial class ContractNotificationsModule : AppModuleBase
    {
        public override LogCat Category => LogCat.ContractNotif;
        private int _checkInterval;
        private DateTime _lastCheckTime = DateTime.MinValue;

        private readonly ConcurrentDictionary<long, string> _etokens = new ConcurrentDictionary<long, string>();
        private readonly ConcurrentDictionary<long, string> _corpEtokens = new ConcurrentDictionary<long, string>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Contracts module...", Category);
            _checkInterval = Settings.ContractNotificationsModule.CheckIntervalInMinutes;
            if (_checkInterval == 0)
                _checkInterval = 1;

            await WebPartInitialization();

            var data = Settings.ContractNotificationsModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.CharacterEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);
        }

        private static readonly List<string> _completeStatuses = new List<string> {"finished_issuer", "finished_contractor", "finished", "cancelled", "rejected", "failed", "deleted", "reversed"};
        private static readonly List<string> _finishedStatuses = new List<string> {"finished_issuer", "finished_contractor", "finished"};
        private static readonly List<string> _rejectedStatuses = new List<string> { "cancelled", "rejected", "failed", "deleted", "reversed"};
        private static readonly List<string> _activeStatuses = new List<string> {"in_progress", "outstanding"};

        public override async Task Run(object prm)
        {
            if (IsRunning || !Settings.Config.ModuleContractNotifications || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;
            try
            {
                if ((DateTime.Now - _lastCheckTime).TotalMinutes < _checkInterval) return;
                _lastCheckTime = DateTime.Now;
                await LogHelper.LogModule("Running Contracts module check...", Category);

                foreach (var (name, group) in Settings.ContractNotificationsModule.GetEnabledGroups())
                {
                    var chars = GetParsedCharacters(name) ?? new List<long>();
                    foreach (var characterID in chars)
                    {
                        if(characterID <=0) continue;
                        try
                        {
                            var rtoken = await DbHelper.GetToken(characterID, TokenEnum.Contract);
                            if (rtoken == null)
                            {
                                await SendOneTimeWarning(characterID, $"Contracts feed token for character {characterID} not found! User is not authenticated.");
                                continue;
                            }

                            if(rtoken.Scopes == null) continue;

                            var s = new ESIScope().AddUniverseStructure();
                            if (SettingsManager.HasCharContractsScope(rtoken.Scopes.Split(',').ToList()))
                                s.AddCharContracts();
                            if (SettingsManager.HasCorpContractsScope(rtoken.Scopes.Split(',').ToList()))
                                s.AddCorpContracts();

                            var tq = await APIHelper.ESIAPI.GetAccessTokenWithScopes(rtoken, s,$"From {Category} | Char ID: {characterID}");
                            var token = tq.Result;
                            if (string.IsNullOrEmpty(token))
                            {
                                if (tq.Data.IsNotValid && !tq.Data.IsNoConnection)
                                {
                                    await LogHelper.LogWarning(
                                        $"Contracts token for character {characterID} is outdated or no more valid!",
                                        Category);

                                    await LogHelper.LogWarning($"Deleting invalid mail refresh token for {characterID}", Category);
                                    await DbHelper.DeleteToken(characterID, TokenEnum.Contract);
                                }
                                else
                                    await LogHelper.LogWarning($"Unable to get contracts token for character {characterID}. Current check cycle will be skipped. {tq.Data.ErrorCode}({tq.Data.Message})", Category);

                                continue;
                            }

                            if (group.FeedPersonalContracts)
                            {
                                await ProcessContracts(false, group, characterID, token);
                            }
                            if (group.FeedCorporateContracts)
                            {
                                await ProcessContracts(true, group, characterID, token);
                            }

                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx("Contracts", ex, Category);
                        }
                    }
                }
                // await LogHelper.LogModule("Completed", Category);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
               // await LogHelper.LogModule("Completed", Category);
            }
            finally
            {
                IsRunning = false; 
            }
        }

        private async Task ProcessContracts(bool isCorp, ContractNotifyGroup group, long characterID, string token)
        {
            if(group == null) return;
            var maxContracts = Settings.ContractNotificationsModule.MaxTrackingCount > 0 ? Settings.ContractNotificationsModule.MaxTrackingCount : 150;
            List<JsonClasses.Contract> contracts;

            var corpID = isCorp ? (await APIHelper.ESIAPI.GetCharacterData(Reason, characterID))?.corporation_id ?? 0 : 0;
            if (isCorp)
            {
                var etag = _corpEtokens.GetOrNull(characterID);
                var result = await APIHelper.ESIAPI.GetCorpContracts(Reason, corpID, token, etag);
                if(result?.Data == null || result.Data.IsNotModified) return;
                _corpEtokens.AddOrUpdateEx(characterID, result.Data.ETag);

                contracts = result.Result?.OrderByDescending(a => a.contract_id).ToList();
            }
            else
            {
                var etag = _etokens.GetOrNull(characterID);
                var result = await APIHelper.ESIAPI.GetCharacterContracts(Reason, characterID, token, etag);
                if(result?.Data == null || result.Data.IsNotModified) return;
                _etokens.AddOrUpdateEx(characterID, result.Data.ETag);

                contracts = result.Result?.OrderByDescending(a => a.contract_id).ToList();
            }

            if (contracts == null || !contracts.Any())
                return;

            var lastContractId = contracts.FirstOrDefault()?.contract_id ?? 0;
            if (lastContractId == 0) return;

            var lst = await DbHelper.GetContracts(characterID, isCorp);
            var otherList = isCorp ? await DbHelper.GetContracts(characterID, false) : null;

            if (lst == null)
            {
                lst = new List<JsonClasses.Contract>(contracts.Where(a=> _activeStatuses.ContainsCaseInsensitive(a.status)).TakeSmart(maxContracts));
                await DbHelper.SaveContracts(characterID, lst, isCorp);
                return;
            }

            //process cache
            foreach (var contract in lst.ToList())
            {
                var freshContract = contracts.FirstOrDefault(a => a.contract_id == contract.contract_id);
                //check if it present
                if (freshContract == null)
                {
                    lst.Remove(contract);
                    continue;                                        
                }

                if(group.Filters == null) continue;
                foreach (var filter in group.Filters.Values)
                {
                    if(filter.Types.Any() && !filter.Types.Contains(contract.type))
                        continue;

                    if (filter.Availability.Any() && !filter.Availability.ContainsCaseInsensitive(contract.availability))
                        continue;

                    //check for completion
                    if (_completeStatuses.Contains(freshContract.status) && filter.Statuses.Contains(freshContract.status))
                    {
                        if (filter.DiscordChannelId > 0 && APIHelper.DiscordAPI.GetChannel(filter.DiscordChannelId) != null)
                            await PrepareFinishedDiscordMessage(filter.DiscordChannelId, freshContract, group.DefaultMention, isCorp, characterID, corpID, token, filter);
                        else
                            await LogHelper.LogWarning($"Specified filter channel ID: {filter.DiscordChannelId} is not accessible!", Category);
                        await LogHelper.LogModule($"--> Contract {freshContract.contract_id} is {freshContract.status}!", Category);
                        if(lst.Contains(contract))
                            lst.Remove(contract);
                        continue;
                    }
                    //check for accepted
                    if (contract.type == "courier" && contract.status == "outstanding" && freshContract.status == "in_progress" && filter.Statuses.Contains("in_progress"))
                    {
                        await PrepareAcceptedDiscordMessage(filter.DiscordChannelId, freshContract, group.DefaultMention, isCorp, characterID, corpID, token, filter);
                        var index = lst.IndexOf(contract);
                        lst.Remove(contract);
                        lst.Insert(index < 0 ? 0 : index, freshContract);
                        await LogHelper.LogModule($"--> Contract {freshContract.contract_id} is accepted!", Category);
                        continue;
                    }
                }

            }

            
            //silently remove filtered out expired contracts
            var lefties = lst.Where(a => _completeStatuses.Contains(a.status)).ToList();
            foreach (var lefty in lefties)
            {
                lst.Remove(lefty);
            }

            //update cache list and look for new contracts
            var lastRememberedId = lst.FirstOrDefault()?.contract_id ?? 0;
            if (lastContractId > lastRememberedId)
            {
                //get and report new contracts, forget already finished
                var list = contracts.Where(a => a.contract_id > lastRememberedId && !_completeStatuses.Contains(a.status)).ToList();
                if (otherList != null)
                {
                    list = list.Where(a => otherList.All(b => b.contract_id != a.contract_id)).ToList();
                }

                //fix loop
                foreach (var contract in list)
                {
                    var isCharAssignee = await APIHelper.ESIAPI.GetCharacterData(Reason, contract.assignee_id) != null;
                    bool isCorpAssignee = false;
                    bool isAllyAssignee = false;
                    if (!isCharAssignee)
                    {
                        isCorpAssignee = await APIHelper.ESIAPI.GetCorporationData(Reason, contract.assignee_id) != null;
                        isAllyAssignee = !isCorpAssignee;
                    }

                    contract.availability = isCharAssignee ? "personal" : (isCorpAssignee ? "corporation" : "alliance");
                }

                bool stop = false;
                foreach (var contract in list)
                {
                    foreach (var (filterName, filter) in group.Filters)
                    {
                        if (stop) break;
                        if (!filter.Statuses.Contains(contract.status)) continue;
                        //types
                        if (filter.Types.Any() && !filter.Types.Contains(contract.type)) continue;
                        //availability
                        if (filter.Availability.Any() && !filter.Availability.ContainsCaseInsensitive(contract.availability)) continue;

                        //filter by issue target
                        if (!filter.FeedIssuedBy)
                        {
                            if (isCorp)
                            {
                                if(contract.for_corporation && contract.issuer_corporation_id == corpID)
                                    continue;
                            }
                            else
                            {
                                if(contract.issuer_id == characterID) continue;
                            }

                        }

                        if (!filter.FeedIssuedTo)
                        {
                            if (isCorp)
                                if(contract.assignee_id == corpID) continue;
                            else if(contract.assignee_id == characterID) continue;
                        }

                        try
                        {
                            await LogHelper.LogModule($"--> New Contract {contract.contract_id} found!", Category);
                            if (filter.DiscordChannelId != 0)
                                await PrepareDiscordMessage(filter.DiscordChannelId, contract, group.DefaultMention, isCorp, characterID, corpID, token, filter);
                            if (group.StopOnFirstFilterMatch)
                                stop = true;
                        }
                        catch (Exception ex)
                        {
                            await LogHelper.LogEx($"Contract {contract.contract_id}", ex, Category);

                        }
                    }
                }

                if (list.Count > 0)
                {
                    lst.InsertRange(0, list);
                    //cut
                    if (lst.Count >= maxContracts)
                    {
                        var count = lst.Count - maxContracts;
                        lst.RemoveRange(lst.Count - count, count);
                    }
                }

 
            }

            //kill dupes
            var rr = lst.GroupBy(a => a.contract_id).Where(a => a.Count() > 1).Select(a=> a.Key).Distinct();
            foreach (var item in rr)
            {
                var o = lst.FirstOrDefault(a => a.contract_id == item);
                if (o != null)
                    lst.Remove(o);
            }

            await DbHelper.SaveContracts(characterID, lst, isCorp);

        }


        private async Task PrepareAcceptedDiscordMessage(ulong channelId, JsonClasses.Contract contract, string mention, bool isCorp, long characterId, long corpId, string token,
            ContractNotifyFilter filter)
        {
            await PrepareDiscordMessage(channelId, contract, mention, isCorp, characterId, corpId, token, filter);

        }

        private async Task PrepareFinishedDiscordMessage(ulong channelId, JsonClasses.Contract contract, string mention, bool isCorp, long characterId, long corpId, string token,
            ContractNotifyFilter filter)
        {
            await PrepareDiscordMessage(channelId, contract, mention, isCorp, characterId, corpId, token, filter);
        }

        private async Task PrepareDiscordMessage(ulong channelId, JsonClasses.Contract contract, string mention, bool isCorp, long characterId, long corpId, string token,
            ContractNotifyFilter filter)
        {
            var image = string.Empty;
            var typeName = string.Empty;
            uint color = 0xff0000;
            switch (contract.status)
            {
                //finished
                case var s when _finishedStatuses.Contains(s):
                    image = Settings.Resources.ImgContractDelete;
                    break;
                default:
                    image = Settings.Resources.ImgContract;
                    break;
            }


            var statusName = string.Empty;
            switch (contract.status)
            {
                case "finished_issuer":
                case "finished_contractor":
                case "finished":
                    statusName = "Completed";
                    color = 0x00ff00;
                    break;
                case "cancelled":
                    statusName = "Cancelled";
                    break;
                case "rejected":
                    statusName = "Rejected";
                    break;
                case "failed":
                    statusName = "Failed";
                    break;
                case "deleted":
                    statusName = "Deleted";
                    break;
                case "reversed":
                    statusName = "Reversed";
                    break;
                case "in_progress":
                    statusName = "In Progress";
                    color = 0xFFFF33;
                    break;
                case "outstanding":
                    statusName = "Outstanding";
                    color = 0xFFFF33;
                    break;
                default:
                    return;
            }

            var days = 0;
            var expire = 0;
            var endLocation = string.Empty;
            switch (contract.type)
            {
                case "item_exchange":
                    typeName = LM.Get("contractTypeExchange");
                    break;
                case "auction":
                    typeName = LM.Get("contractTypeAuction");
                    break;
                case "courier":
                    typeName = LM.Get("contractTypeCourier");
                    days = contract.days_to_complete;
                    expire = (int) (contract.DateExpired - contract.DateIssued).Value.TotalDays;
                    endLocation = (await APIHelper.ESIAPI.GetUniverseStructureData(Reason, contract.end_location_id, token))?.name ??
                                  (await APIHelper.ESIAPI.GetStationData(Reason, contract.end_location_id, token))?.name;
                    endLocation = string.IsNullOrEmpty(endLocation) ? LM.Get("contractSomeCitadel") : endLocation;
                    break;
                default:
                    return;
            }

            var subject = $"{typeName} {LM.Get("contractSubject")}";
            var title = string.IsNullOrEmpty(contract.title) ? "-" : contract.title;

            var ch = await APIHelper.ESIAPI.GetCharacterData(Reason, contract.issuer_id);
            var issuerName = $"[{ch.name}](https://zkillboard.com/character/{contract.issuer_id}/)";
            if (contract.for_corporation)
            {
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, contract.issuer_corporation_id);
                issuerName = $"[{corp.name}](https://zkillboard.com/corporation/{contract.issuer_corporation_id}/)";
            }

            var ach = await APIHelper.ESIAPI.GetCharacterData(Reason, contract.assignee_id);
            var asigneeName = "public";
            if (ach != null)
                asigneeName = $"[{ach.name}](https://zkillboard.com/character/{contract.assignee_id}/)";
            else
            {
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, contract.assignee_id);
                if (corp != null)
                    asigneeName = $"[{corp.name}](https://zkillboard.com/corporation/{contract.assignee_id}/)";
                else
                {
                    var ally = await APIHelper.ESIAPI.GetAllianceData(Reason, contract.assignee_id);
                    if (ally != null)
                        asigneeName = $"[{ally.name}](https://zkillboard.com/alliance/{contract.assignee_id}/)";
                }
            }

            //location

            var startLocation = (await APIHelper.ESIAPI.GetUniverseStructureData(Reason, contract.start_location_id, token))?.name ??
                                (await APIHelper.ESIAPI.GetStationData(Reason, contract.start_location_id, token))?.name;
            startLocation = string.IsNullOrEmpty(startLocation) ? LM.Get("contractSomeCitadel") : startLocation;
            var locationText = LM.Get("contractMsgIssued");
            locationText = string.IsNullOrWhiteSpace(locationText) ? "-" : locationText;


            var embed = new EmbedBuilder();
            if (filter.ShowOnlyBasicDetails)
            {
                embed.WithThumbnailUrl(image)
                    .WithColor(color)
                    .AddField(subject, title, true)
                    .AddField(LM.Get("contractMsgIssued"), $"{LM.Get("simpleFrom").FirstLetterToUpper()} {issuerName} {LM.Get("simpleTo")} {asigneeName}", true)
                    .AddField(LM.Get("contractMsgStatus"), $"**{statusName}**", true)
                    .AddField(locationText, startLocation, true);

                if (contract.type == "courier")
                    embed.AddField($"{LM.Get("contractMsgCollateral")}/{LM.Get("contractMsgReward")}", $"{contract.collateral:N} / {contract.reward:N}", true);
                else
                {
                    if (contract.price > 0 || contract.reward > 0)
                        embed.AddField(LM.Get(contract.price > 0 ?"contractMsgPrice": "contractMsgReward"),  contract.price > 0 ? $"{contract.price:N}" : $"{contract.reward:N}");
                    else if (contract.type == "auction")
                        embed.AddField(LM.Get("contractMsgBuyout"), $"{contract.buyout:N}");
                }

                if (contract.volume > 0)
                {
                    embed.AddField(LM.Get("contractMsgVolume"), $"{contract.volume:N1} m3", true);
                }
                if (filter.ShowIngameOpen)
                    embed.WithDescription($"[{LM.Get("contractOpenIngame")}]({ServerPaths.GetOpenContractURL(contract.contract_id)})");
            }
            else
            {
                var sbNames = new StringBuilder();
                var sbValues = new StringBuilder();

                sbNames.Append($"{LM.Get("contractMsgType")}: \n{LM.Get("contractMsgIssuedBy")}: \n{LM.Get("contractMsgIssuedTo")}: ");
                if (contract.acceptor_id > 0)
                    sbNames.Append($"\n{LM.Get("contractMsgContractor")}: ");
                sbNames.Append($"\n{LM.Get("contractMsgStatus")}: ");
                if (contract.type == "courier")
                    sbNames.Append(
                        $"\n{LM.Get("contractMsgCollateral")}: \n{LM.Get("contractMsgReward")}: \n{LM.Get("contractMsgCompleteIn")}: \n{LM.Get("contractMsgExpireIn")}: ");
                else
                {
                    if (contract.price > 0) sbNames.Append($"\n{LM.Get("contractMsgPrice")}: ");
                    else if (contract.reward > 0) sbNames.Append($"\n{LM.Get("contractMsgReward2")}: ");

                    if (contract.type == "auction")
                        sbNames.Append($"\n{LM.Get("contractMsgBuyout")}: ");
                }

                sbValues.Append($"{typeName}\n{issuerName}\n{asigneeName}");
                if (contract.acceptor_id > 0)
                {
                    ch = await APIHelper.ESIAPI.GetCharacterData(Reason, contract.acceptor_id);
                    sbValues.Append($"\n[{(ch?.name ?? LM.Get("Unknown"))}](https://zkillboard.com/character/{contract.acceptor_id}/)");
                }

                sbValues.Append($"\n**{statusName}**");
                if (contract.type == "courier")
                    sbValues.Append($"\n{contract.collateral:N}\n{contract.reward:N}\n{days} {LM.Get("contractMsgDays")}\n{expire} {LM.Get("contractMsgDays")}");
                else
                {
                    if (contract.price > 0 || contract.reward > 0)
                        sbValues.Append(contract.price > 0 ? $"\n{contract.price:N}" : $"\n{contract.reward:N}");
                    if (contract.type == "auction")
                        sbValues.Append($"\n{contract.buyout:N}");
                }

                var stampIssued = contract.DateIssued?.ToString(Settings.Config.ShortTimeFormat);
                var stampAccepted = contract.DateAccepted?.ToString(Settings.Config.ShortTimeFormat);
                var stampCompleted = contract.DateCompleted?.ToString(Settings.Config.ShortTimeFormat);
                var stampExpired = contract.DateExpired?.ToString(Settings.Config.ShortTimeFormat);

                var items = isCorp
                    ? await APIHelper.ESIAPI.GetCorpContractItems(Reason, corpId, contract.contract_id, token)
                    : await APIHelper.ESIAPI.GetCharacterContractItems(Reason, characterId, contract.contract_id, token);

                // var x2 =  await APIHelper.ESIAPI.GetPublicContractItems(Reason, contract.contract_id);
                items = items ?? await APIHelper.ESIAPI.GetCharacterContractItems(Reason, contract.issuer_id, contract.contract_id, token);
                var sbItemsSubmitted = new StringBuilder();
                var sbItemsAsking = new StringBuilder();
                if (items != null && items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        var t = await APIHelper.ESIAPI.GetTypeId(Reason, item.type_id);
                        if (item.is_included)
                        {
                            sbItemsSubmitted.Append($"{t?.Name} x{item.quantity}\n");
                        }
                        else sbItemsAsking.Append($"{t?.Name} x{item.quantity}\n");
                    }
                }

                if (contract.volume > 0)
                {
                    sbNames.Append($"\n{LM.Get("contractMsgVolume")}: ");
                    sbValues.Append($"\n{contract.volume:N1} m3");
                }

                var issuedText = $"{LM.Get("contractMsgIssued")}: {stampIssued}";


                //EMBED

                embed.WithThumbnailUrl(image)
                    .WithColor(color)
                    .AddField(subject, title)
                    .AddField(locationText, startLocation);

                if (filter.ShowIngameOpen)
                {
                    embed.WithDescription($"[{LM.Get("contractOpenIngame")}]({ServerPaths.GetOpenContractURL(contract.contract_id)})\n");
                }

                if (contract.type == "courier")
                    embed.AddField(LM.Get("contractMsgDestination"), endLocation);

                embed.AddField(LM.Get("contractMsgDetails"), sbNames.ToString(), true)
                    .AddField("-", sbValues.ToString(), true)
                    .WithFooter(issuedText);

                if (sbItemsSubmitted.Length > 0)
                {
                    var fields = sbItemsSubmitted.ToString().Split(1023).TakeSmart(5).ToList();
                    var head = fields.FirstOrDefault();
                    fields.RemoveAt(0);
                    embed.AddField(LM.Get("contractMsgIncludedItems"), string.IsNullOrWhiteSpace(head) ? "---" : head);
                    foreach (var field in fields)
                        embed.AddField($"-", string.IsNullOrWhiteSpace(field) ? "---" : field);
                }

                if (sbItemsAsking.Length > 0)
                {
                    var fields = sbItemsAsking.ToString().Split(1023).TakeSmart(5).ToList();
                    var head = fields.FirstOrDefault();
                    fields.RemoveAt(0);
                    embed.AddField(LM.Get("contractMsgAskingItems"), string.IsNullOrWhiteSpace(head) ? "---" : head);
                    foreach (var field in fields)
                        embed.AddField($"-", string.IsNullOrWhiteSpace(field) ? "---" : field);
                }

            }

            if (filter.RedirectByIdInDescription && !string.IsNullOrEmpty(contract.title))
            {
                var result = ulong.TryParse(contract.title.Trim(), out var feedId);
                if (!result)
                {
                    var arr = contract.title.Split(' ');
                    if(arr.Length > 1)
                        result = ulong.TryParse(arr[arr.Length-1].Trim(), out feedId);
                }

                if (result)
                {
                    var user = APIHelper.DiscordAPI.GetUser(feedId);
                    if (user != null)
                    {
                        await user.SendMessageAsync(">>>\n", false, embed.Build());
                        if(!filter.PostToChannelIfRedirected)
                            return;
                    }
                }
            }

            await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(channelId), $"{mention} >>>\n", embed.Build()).ConfigureAwait(false);
        }

        public static async Task<string[]> GetContractItemsString(string reason, bool isCorp, long corpId, long charId, long contractId, string token)
        {
            var sbItemsSubmitted = new StringBuilder();
            var sbItemsAsking = new StringBuilder();

            var items = isCorp ?
                await APIHelper.ESIAPI.GetCorpContractItems(reason, corpId, contractId, token) :
                await APIHelper.ESIAPI.GetCharacterContractItems(reason, charId, contractId, token);

            if (items != null && items.Count > 0)
            {
                foreach (var item in items)
                {
                    var t = await APIHelper.ESIAPI.GetTypeId(reason, item.type_id);
                    if(item.is_included)
                        sbItemsSubmitted.Append($"{t?.Name} x{item.quantity}\n");
                    else sbItemsAsking.Append($"{t?.Name} x{item.quantity}\n");
                }
            }

            return new[] {sbItemsSubmitted.ToString(), sbItemsAsking.ToString()};
        }

        public static async Task ProcessClistCommand(ICommandContext context, KeyValuePair<string, ContractNotifyGroup> groupPair, string mod)
        {
            try
            {
                var group = groupPair.Value;
                var personalContracts = new List<JsonClasses.Contract>();
                var corpContracts = new List<JsonClasses.Contract>();

                var chars = TickManager.GetModule<ContractNotificationsModule>().GetParsedCharacters(groupPair.Key);

                foreach (var characterID in chars)
                {       
                    if(group.FeedPersonalContracts)
                        personalContracts.AddRange(await DbHelper.GetContracts(characterID, false));
                    if(group.FeedCorporateContracts)
                        corpContracts.AddRange(await DbHelper.GetContracts(characterID, true));
                }

                if (mod.Length > 1)
                {
                    var type = mod[1];
                    switch (char.ToLower(type))
                    {
                        case 'c': //courier
                            personalContracts.RemoveAll(a => !a.type.Equals("courier", StringComparison.OrdinalIgnoreCase));
                            corpContracts.RemoveAll(a => !a.type.Equals("courier", StringComparison.OrdinalIgnoreCase));
                            break;
                        case 'e': //exchange
                            personalContracts.RemoveAll(a => !a.type.Equals("item_exchange", StringComparison.OrdinalIgnoreCase));
                            corpContracts.RemoveAll(a => !a.type.Equals("item_exchange", StringComparison.OrdinalIgnoreCase));
                            break;
                        case 'a': //auction
                            personalContracts.RemoveAll(a => !a.type.Equals("auction", StringComparison.OrdinalIgnoreCase));
                            corpContracts.RemoveAll(a => !a.type.Equals("auction", StringComparison.OrdinalIgnoreCase));
                            break;
                        case '0':
                            break;
                        default:
                            await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("helpClist"), true);
                            return;
                    }

                    if (mod.Length > 2)
                    {
                        var avail = mod[2];
                        switch (char.ToLower(avail))
                        {
                            case 'a': //alliance
                                personalContracts.RemoveAll(a => !a.availability.Equals("alliance", StringComparison.OrdinalIgnoreCase));
                                corpContracts.RemoveAll(a => !a.availability.Equals("alliance", StringComparison.OrdinalIgnoreCase));
                                break;
                            case 'c': //exchange
                                personalContracts.RemoveAll(a => !a.availability.Equals("corporation", StringComparison.OrdinalIgnoreCase));
                                corpContracts.RemoveAll(a => !a.availability.Equals("corporation", StringComparison.OrdinalIgnoreCase));
                                break;
                            case 'p': //public
                                personalContracts.RemoveAll(a => !a.availability.Equals("public", StringComparison.OrdinalIgnoreCase));
                                corpContracts.RemoveAll(a => !a.availability.Equals("public", StringComparison.OrdinalIgnoreCase));
                                break;
                            case '0':
                                break;
                            default:
                                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("helpClist"), true);
                                return;
                        }
                    }
                }


                switch (char.ToLower(mod[0]))
                {
                    case 'o':
                        personalContracts = personalContracts.Where(a => _activeStatuses.Contains(a.status)).ToList();
                        corpContracts = corpContracts.Where(a => _activeStatuses.Contains(a.status)).ToList();
                        break;
                    case 'c':
                        personalContracts = personalContracts.Where(a => _completeStatuses.Contains(a.status)).ToList();
                        corpContracts = corpContracts.Where(a => _completeStatuses.Contains(a.status)).ToList();
                        break;
                    case 'f':
                        personalContracts = personalContracts.Where(a => _finishedStatuses.Contains(a.status)).ToList();
                        corpContracts = corpContracts.Where(a => _finishedStatuses.Contains(a.status)).ToList();
                        break;
                    case 'r':
                        personalContracts = personalContracts.Where(a => _rejectedStatuses.Contains(a.status)).ToList();
                        corpContracts = corpContracts.Where(a => _rejectedStatuses.Contains(a.status)).ToList();
                        break;
                    case 'a':
                        break;
                    default:
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("helpClist"), true);
                        return;
                }

                var sb = new StringBuilder();
                if (personalContracts.Any())
                {
                    sb.AppendLine("");
                    sb.AppendLine(LM.Get("clistPersonalTitle"));
                    sb.AppendLine("```");
                    //header
                    sb.Append(LM.Get("clistHeaderName").FixedLength(20));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderType").FixedLength(13));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderStatus").FixedLength(11));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderExp").FixedLength(12));
                    sb.Append(Environment.NewLine);

                    foreach (var contract in personalContracts)
                    {
                        sb.Append((string.IsNullOrEmpty(contract.title) ? "-" : contract.title).FixedLength(20));
                        sb.Append("  ");
                        sb.Append(contract.type.FixedLength(13));
                        sb.Append("  ");
                        sb.Append(contract.status.FixedLength(11));
                        sb.Append("  ");
                        if (_activeStatuses.Contains(contract.status))
                        {
                            var value = (contract.DateExpired - DateTime.UtcNow).Value;
                            sb.Append($"{value.Days}d {value.Hours}h {value.Minutes}m");
                        }
                        else
                            sb.Append(LM.Get("clistExpired"));
                        sb.Append(Environment.NewLine);
                    }
                    sb.AppendLine("```");
                }
                if (corpContracts.Any())
                {
                    sb.AppendLine("");
                    sb.AppendLine(LM.Get("clistCorpTitle"));
                    sb.AppendLine("```");
                    //header
                    sb.Append(LM.Get("clistHeaderName").FixedLength(20));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderType").FixedLength(13));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderStatus").FixedLength(11));
                    sb.Append("  ");
                    sb.Append(LM.Get("clistHeaderExp").FixedLength(12));
                    sb.Append(Environment.NewLine);

                    foreach (var contract in corpContracts)
                    {
                        sb.Append((string.IsNullOrEmpty(contract.title) ? "-" : contract.title).FixedLength(20));
                        sb.Append("  ");
                        sb.Append(contract.type.FixedLength(13));
                        sb.Append("  ");
                        sb.Append(contract.status.FixedLength(11));
                        sb.Append("  ");
                        if (_activeStatuses.Contains(contract.status))
                        {
                            var value = (contract.DateExpired - DateTime.UtcNow).Value;
                            sb.Append($"{value.Days}d {value.Hours}h {value.Minutes}m");
                        }
                        else
                            sb.Append(LM.Get("clistExpired"));
                        sb.Append(Environment.NewLine);
                    }
                    sb.AppendLine("```");
                }

                if (!personalContracts.Any() && !corpContracts.Any())
                    sb.Append(LM.Get("clistNoContracts"));

                await APIHelper.DiscordAPI.ReplyMessageAsync(context, sb.ToString(), true);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(ProcessClistCommand), ex, LogCat.ContractNotif);
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("WebRequestUnexpected"));
            }
        }

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleContractNotifications) return false;
            var m = TickManager.GetModule<ContractNotificationsModule>();
            return m?.GetAllParsedCharacters().Contains(id) ?? false;
        }

    }
}
