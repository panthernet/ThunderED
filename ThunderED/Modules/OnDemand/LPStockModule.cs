using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    internal static class LPStockModule
    {
        internal static async Task<List<LPStockEntry>> GetLPStockInformation(long corpId, int maxCount = 10)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(webClient.DownloadString($"http://lpstock.ru/corp.php?corpid={corpId}"));

                    var table = doc.DocumentNode.SelectSingleNode("//table[@class='tablesorter']")
                        .Descendants("tr")
                        //.Skip(1)
                        .Where(tr => tr.Elements("td").Count() > 1)
                        .Select(tr => tr.Elements("td").Select(td => td.InnerText.Trim()).ToList())
                        .ToList();
                    return table.Select(list =>
                    {
                        var item = new LPStockEntry
                        {
                            Name = list[0].Split('\n')[0],
                            SellPrice = Convert.ToDecimal(list[5]),
                            Ratio = Convert.ToDouble(list[6])
                        };
                        return item;
                    }).OrderByDescending(a => a.Ratio).Take(maxCount).ToList();

                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("LPStock exception", ex);
                return new List<LPStockEntry>();
            }
        }

        public static async Task<bool> SendTopLP(ICommandContext context, string command)
        {
            var data = await FWStatsModule.GetCorpData(command);
            if (data == null)
                return false;

            var avgRatio = 0d;
            var topItems = string.Empty;
            var quantityToFetch = 10;
            var lpstockList = await GetLPStockInformation(data.factionCorpId, quantityToFetch);
            if (lpstockList.Count > 0)
            {
                avgRatio = Math.Round(lpstockList.Average(a => a.Ratio), 1);
                var sb = new StringBuilder();
                for (var i = 0; i < lpstockList.Count && i < quantityToFetch; i++)
                {
                    sb.Append((i + 1).ToString());
                    sb.Append(". ");
                    sb.Append(lpstockList[i].Name);
                    sb.Append(" (");
                    sb.Append(lpstockList[i].Ratio);
                    sb.Append(")\n");
                }

                sb.Append("\n");
                sb.Append($"[{LM.Get("lpStock_PageText")}](http://lpstock.ru/corp.php?corpid={data.factionCorpId})");
                topItems = sb.ToString();
            }

            var embed = new EmbedBuilder()
                .WithTitle(LM.Get("lpStock_title", data.factionName))
                .AddInlineField(LM.Get("lpStock_DataHeader"), LM.Get("lpStock_AverageLp", avgRatio))
                .AddInlineField(LM.Get("lpStock_ItemsHeader"), topItems)
                .WithColor(0x00FF00);

            if (!string.IsNullOrEmpty(data.factionImage))
                embed.WithThumbnailUrl(data.factionImage);

            await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, " ", embed.Build()).ConfigureAwait(false);

            return true;
        }
    }

    internal class LPStockEntry
    {
        public string Name { get; set; }
        public decimal SellPrice { get; set; }
        public double Ratio { get; set; }
    }
}
