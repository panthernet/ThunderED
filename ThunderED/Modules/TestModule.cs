using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class TestModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Default;

        public static async Task DebugKillmailMessage(ICommandContext context, string template, bool isNpcKill = false)
        {
            try
            {
                var lines = (await File.ReadAllLinesAsync(Path.Combine(SettingsManager.RootDirectory, "Templates/Messages/default", "def.Template.killMailGeneral.txt")))
                    .Where(a => !a.StartsWith("//") && !string.IsNullOrWhiteSpace(a)).ToList();
                var dic = new Dictionary<string, string>
                {
                    {"{shipID}", "28848"},
                    {"{shipType}", "Nemesis"},
                    {"{iskValue}", "123 456 789"},
                    {"{systemName}", "Asakai"},
                    {"{systemSec}", "0.4"},
                    {"{victimName}", "Don Chack"},
                    {"{victimCorp}", "Nebula Alba"},
                    {"{victimAllyTicker}", "<UF>"},
                    {"{attackerName}", "Rad1st"},
                    {"{attackerCorp}", "Airguard"},
                    {"{attackerAllyTicker}", "<-LSH->"},
                    {"{attackersCount}", "1"},
                    {"{kmId}", "69474440"},
                    {"{timestamp}", "27.04.2018"},
                };

                foreach (var pair in dic)
                {
                    for (var i = 0; i < lines.Count; i++)
                    {
                        lines[i] = lines[i].Replace(pair.Key, pair.Value).Trim().TrimStart('\t');
                    }
                }

                //xx
                bool isAuthor = false;
                var embed = new EmbedBuilder();
                EmbedAuthorBuilder author = null;
                lines.ForEach(line =>
                {
                    var text = line;
                    if (text.StartsWith("EmbedBuilder")) return;

                    if (text.StartsWith("#if {isNpcKill}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isNpcKill) return;
                        text = text.Substring(15, text.Length - 15);
                    }

                    if (!isAuthor && text.StartsWith(".WithAuthor", StringComparison.OrdinalIgnoreCase))
                    {
                        isAuthor = true;
                        author = new EmbedAuthorBuilder();
                        return;
                    }
                    if (isAuthor && text.StartsWith(")"))
                    {
                        isAuthor = false;
                        embed.WithAuthor(author);
                        return;
                    }

                    if (isAuthor)
                    {
                        if (text.StartsWith(".WithName", StringComparison.OrdinalIgnoreCase))
                            author.WithName(GetText(text));
                        else if (text.StartsWith(".WithUrl", StringComparison.OrdinalIgnoreCase))
                            author.WithUrl(GetText(text));
                        else if (text.StartsWith(".WithIconUrl", StringComparison.OrdinalIgnoreCase))
                            author.WithIconUrl(GetText(text));
                    }
                    else
                    {

                        if (text.StartsWith(".WithColor", StringComparison.OrdinalIgnoreCase))
                            embed.WithColor(GetColor(text));
                        else if (text.StartsWith(".WithDescription(", StringComparison.OrdinalIgnoreCase))
                            embed.WithDescription(GetText(text));
                        else if (text.StartsWith(".WithThumbnailUrl(", StringComparison.OrdinalIgnoreCase))
                            embed.WithThumbnailUrl(GetText(text));
                        else if (text.StartsWith(".AddField"))
                        {
                            var result = GetDoubleText(text);
                            embed.AddField(result[0], result[1]);
                        }
                        else if (text.StartsWith(".AddInlineField", StringComparison.OrdinalIgnoreCase))
                        {
                            var result = GetDoubleText(text);
                            embed.AddInlineField(result[0], result[1]);
                        }
                        else if (text.StartsWith(".WithFooter", StringComparison.OrdinalIgnoreCase))
                            embed.WithFooter(GetText(text));
                        else if (text.StartsWith(".WithTimestamp", StringComparison.OrdinalIgnoreCase))
                            embed.WithTimestamp(DateTimeOffset.Now);
                    }
                });
             //   if (author != null)
             //       embed.WithAuthor(author);
                await APIHelper.DiscordAPI.SendMessageAsync(context, "cc", embed.Build());
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("test", ex, LogCat.Default);
            }
        }

        private static uint GetColor(string text)
        {
            var index = text.IndexOf('(');
            var tex = text.Substring(index + 1, text.Length - index - 3);
            return Convert.ToUInt32(tex, 16);
        }

        private static string GetText(string text)
        {
            var index = text.IndexOf('"');
            var res = text.Substring(index + 1, text.Length - index - 3);
            return TryGetChar(res);
        }

        private static string[] GetDoubleText(string text)
        {
            var start = text.IndexOf('"');
            var end = text.IndexOf('"', start + 1);
            var start2 = text.IndexOf('"', end + 1);
            var end2 = text.IndexOf('"', start2 + 1);
            return new [] { TryGetChar(text.Substring(start+1, end - start -1)), TryGetChar(text.Substring(start2+1, end2-start2 -1))};
        }

        private static string TryGetChar(string text)
        {
            return text == " " ? "\u200b" : text;
        }
    }
}
