using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Helpers;

namespace ThunderED.Classes
{
    public static class TemplateHelper
    {
        public static async Task<bool> PostTemplatedMessage(MessageTemplateType type, Dictionary<string, string> dic, ulong channelId, string message)
        {
            var templateFile = GetTemplate(type);
            if (string.IsNullOrEmpty(templateFile)) return false;
            var embed = await CompileTemplate(type, templateFile, dic);
            if (embed == null) return false;
            var guildID = SettingsManager.GetULong("config", "discordGuildId");
            var discordGuild = APIHelper.DiscordAPI.Client.Guilds.FirstOrDefault(x => x.Id == guildID);
            var channel = discordGuild?.GetTextChannel(channelId);
            if (channel != null) await channel.SendMessageAsync(message, false, embed).ConfigureAwait(false);
            return true;
        }

        public static string GetTemplate(MessageTemplateType type)
        {
            string typeFile;
            switch (type)
            {
                case MessageTemplateType.KillMailBig:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailBig.txt");
                    break;
                case MessageTemplateType.KillMailGeneral:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailGeneral.txt");
                    break;
                case MessageTemplateType.KillMailRadius:
                    typeFile = Path.Combine(SettingsManager.RootDirectory, "Templates/Messages", "Template.killMailRadius.txt");
                    break;
                default:
                    return null;
            }

            if (!string.IsNullOrEmpty(typeFile) && File.Exists(typeFile))
                return typeFile;
            return null;
        }

        public static async Task<Embed> CompileTemplate(MessageTemplateType type, string fileName, Dictionary<string, string> dic)
        {
            try
            {
                var isNpcKill = dic.ContainsKey("isNpcKill") && Convert.ToBoolean(dic["isNpcKill"]);
                var isLoss = dic.ContainsKey("isLoss") && Convert.ToBoolean(dic["isLoss"]);
                if (isNpcKill)
                    dic.Remove("isNpcKill");
                if (isLoss)
                    dic.Remove("isLoss");

                var lines = (await File.ReadAllLinesAsync(fileName))
                    .Where(a => !a.StartsWith("//") && !string.IsNullOrWhiteSpace(a)).ToList();
               
                foreach (var pair in dic)
                {
                    for (var i = 0; i < lines.Count; i++)
                    {
                        lines[i] = lines[i].Replace(pair.Key, pair.Value).Trim().TrimStart('\t');
                    }
                }

                switch (type)
                {
                    case MessageTemplateType.KillMailBig:
                    case MessageTemplateType.KillMailRadius:
                    case MessageTemplateType.KillMailGeneral:
                    {
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
                            if (text.StartsWith("#if !{isNpcKill}", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isNpcKill) return;
                                text = text.Substring(16, text.Length - 16);
                            }

                            if (text.StartsWith("#if {isLoss}", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!isLoss) return;
                                text = text.Substring(12, text.Length - 12);
                            }
                            if (text.StartsWith("#if !{isLoss}", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isLoss) return;
                                text = text.Substring(13, text.Length - 13);
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
                                else if (text.StartsWith(".WithImageUrl(", StringComparison.OrdinalIgnoreCase))
                                    embed.WithImageUrl(GetText(text));
                                else if (text.StartsWith(".WithTitle(", StringComparison.OrdinalIgnoreCase))
                                    embed.WithTitle(GetText(text));
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
                        return embed.Build();
                    }
                        default: return null;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Templates);
                return null;
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
