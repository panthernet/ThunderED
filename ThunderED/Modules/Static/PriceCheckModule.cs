using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.PriceChecks;

namespace ThunderED.Modules.Static
{
    internal class PriceCheckModule: AppModuleBase
    {
        public override LogCat Category => LogCat.PriceCheck;
        public override Task Run(object prm)
        {
            return Task.CompletedTask;
        }

        public static async Task Check(ICommandContext context, string command, string system)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);


                var value = command.ToLower().StartsWith("search")
                    ? command.TrimStart(new char[] {'s', 'e', 'a', 'r', 'c', 'h'})
                    : command;

                var token = await APIHelper.ESIAPI.GetSearchTokenString();
                var result =  await APIHelper.ESIAPI.SearchTypeEntity("PriceCheck", value, token);

                if (result == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("ESIFailure"));
                    await Task.CompletedTask;
                    return;
                }


                if (string.IsNullOrWhiteSpace(result.inventory_type?.ToString()))
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("itemNotExist",command));
                else if (result.inventory_type.Count() > 1)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("seeDM"));

                    var channel = await context.Message.Author.CreateDMChannelAsync();

                    var tmp = JsonConvert.SerializeObject(result.inventory_type);
                    var httpContent = new StringContent(tmp);

                    var itemName = await httpClient.PostAsync($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/names/?datasource=tranquility", httpContent);

                    if (!itemName.IsSuccessStatusCode)
                    {
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, channel, LM.Get("ESIFailure")).ConfigureAwait(false);
                        await Task.CompletedTask;
                        itemName?.Dispose();
                        return;
                    }

                    var itemNameResult = await itemName.Content.ReadAsStringAsync();
                    var itemNameResults = JsonConvert.DeserializeObject<List<JsonClasses.SearchName>>(itemNameResult);
                    itemName?.Dispose();

                    await LogHelper.LogInfo($"Sending {context.Message.Author}'s Price check to {channel.Name}", LogCat.PriceCheck);
                    var builder = new EmbedBuilder()
                        .WithColor(new Color(0x00D000))
                        .WithAuthor(author =>
                        {
                            author
                                .WithName(LM.Get("manyItemsFound"));
                        })
                        .WithDescription(LM.Get("searchExample"));
                    var count = 0;
                    foreach (var inventoryType in result.inventory_type)
                    {
                        if (count < 25)
                        {
                            builder.AddField($"{itemNameResults.FirstOrDefault(x => x.id == inventoryType).name}", "\u200b");
                        }
                        else
                        {
                            var embed2 = builder.Build();

                            await APIHelper.DiscordAPI.SendMessageAsync(channel, "", embed2).ConfigureAwait(false);

                            builder.Fields.Clear();
                            count = 0;
                        }

                        count++;
                    }

                    var embed = builder.Build();
                    await APIHelper.DiscordAPI.SendMessageAsync(channel, "", embed).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        var httpContent = new StringContent($"[{result.inventory_type[0]}]", Encoding.UTF8, "application/json");
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var itemName = await httpClient.PostAsync($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/names/?datasource=tranquility", httpContent);

                        if (!itemName.IsSuccessStatusCode)
                        {
                            await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("ESIFailure"));
                            await Task.CompletedTask;
                            itemName?.Dispose();
                            return;
                        }

                        var itemNameResult = await itemName.Content.ReadAsStringAsync();
                        var itemNameResults = JsonConvert.DeserializeObject<List<JsonClasses.SearchName>>(itemNameResult)[0];
                        itemName?.Dispose();

                        await GoFuzz(httpClient, context, system, result.inventory_type, itemNameResults);

                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx(ex.Message, ex, LogCat.PriceCheck);
                    }
                }
            }
            catch (Exception ex)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, "ERROR Please inform Discord/Bot Owner");
                await LogHelper.LogEx(ex.Message, ex, LogCat.PriceCheck);
            }
        }

        private static async Task GoFuzz(HttpClient httpClient, ICommandContext context, string system,
            List<long> idList, JsonClasses.SearchName itemNameResults)
        {
            var url = "https://market.fuzzwork.co.uk/aggregates/";

            var systemAddon = string.Empty;
            var systemTextAddon = string.IsNullOrEmpty(system) ? null : $"{LM.Get("fromSmall")} {system}";
            switch (system?.ToLower())
            {
                default:
                    systemAddon = "?station=60003760";
                    break;
                case "amarr":
                    systemAddon = "?station=60008494";
                    break;
                case "rens":
                    systemAddon = "?station=60004588";
                    break;
                case "dodixie":
                    systemAddon = "?station=60011866";
                    break;
            }

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
            var webReply = await httpClient.GetStringAsync($"{url}{systemAddon}&types={idList[0]}");
            var market = JsonConvert.DeserializeObject<Dictionary<string,JsonFuzz.FuzzItems>>(webReply);

            await LogHelper.LogInfo($"Sending {context.Message.Author}'s Price check", LogCat.PriceCheck);
            foreach (var marketReply in market.Values)
            {
                var builder = new EmbedBuilder()
                    .WithColor(new Color(0x00D000))
                    .WithThumbnailUrl($"https://image.eveonline.com/Type/{itemNameResults.id}_64.png")
                    .WithAuthor(author =>
                    {
                        author
                            .WithName($"{LM.Get("Item")}: {itemNameResults.name}")
                            .WithUrl($"https://www.fuzzwork.co.uk/info/?typeid={itemNameResults.id}/");
                    })
                    .WithDescription($"{LM.Get("Prices")} {systemTextAddon}")
                    .AddField(LM.Get("Buy"), $"{LM.Get("marketHigh")}: {marketReply.buy.max:N2}{Environment.NewLine}" +
                                             $"{LM.Get("marketMid")}: {marketReply.buy.weightedAverage:N2}{Environment.NewLine}" +
                                             $"{LM.Get("marketLow")}: {marketReply.buy.min:N2}{Environment.NewLine}" +
                                             $"{LM.Get("Volume")}: {marketReply.buy.volume}", true)
                    .AddField(LM.Get("Sell"), $"{LM.Get("marketLow")}: {marketReply.sell.min:N2}{Environment.NewLine}" +
                                              $"{LM.Get("marketMid")}: {marketReply.sell.weightedAverage:N2}{Environment.NewLine}" +
                                              $"{LM.Get("marketHigh")}: {marketReply.sell.max:N2}{Environment.NewLine}" +
                                              $"{LM.Get("Volume")}: {marketReply.sell.volume:N0}", true);

                var embed = builder.Build();
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, "", embed).ConfigureAwait(false);
                await Task.Delay(500);
            }
        }
    }
}
