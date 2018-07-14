using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

    }

    internal class LPStockEntry
    {
        public string Name { get; set; }
        public decimal SellPrice { get; set; }
        public double Ratio { get; set; }
    }
}
