﻿using System;
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
        public static async Task<bool> PostTemplatedMessage(string templateFile, Dictionary<string, string> dic, List<ulong> channelIds, string message)
        {
            var path = Path.Combine(SettingsManager.DataDirectory, "Templates", "Messages", templateFile);
            if (string.IsNullOrEmpty(templateFile) || !File.Exists(path))
                return false;
            var embed = await CompileTemplate(path, dic);
            if (embed == null)
            {
                await LogHelper.LogError($"There was an error compiling '{templateFile}' template for ZKILL!");
                return false;
            }

            if (!channelIds.Any())
                await LogHelper.LogError($"No channels specified for KB feed! Check config.");

            foreach (var id in channelIds)
                await APIHelper.DiscordAPI.SendMessageAsync(id, message, embed).ConfigureAwait(false);

            return true;
        }

        public static async Task<Embed> CompileTemplate(string fileName, Dictionary<string, string> dic)
        {
            try
            {
                var vars = new Dictionary<string, string>();
                var isNpcKill = dic.ContainsKey("{isNpcKill}") && Convert.ToBoolean(dic["{isNpcKill}"]);
                var isLoss = dic.ContainsKey("{isLoss}") && Convert.ToBoolean(dic["{isLoss}"]);
                var isAwox = dic.ContainsKey( "{isAwoxKill}" ) && Convert.ToBoolean( dic["{isAwoxKill}"] );
                var isSolo = dic.ContainsKey( "{isSoloKill}" ) && Convert.ToBoolean( dic["{isSoloKill}"] );
                if (dic.ContainsKey("{isNpcKill}"))
                    dic.Remove("{isNpcKill}");
                if (dic.ContainsKey("{isLoss}"))
                    dic.Remove("{isLoss}");
                if (dic.ContainsKey( "{isAwoxKill}" ))
                    dic.Remove( "{isAwoxKill}" );
                if (dic.ContainsKey( "{isSoloKill}" ))
                    dic.Remove( "{isSoloKill}" );

                var isRadiusRange = dic.ContainsKey("{isRangeMode}") && Convert.ToBoolean(dic["{isRangeMode}"]);
                if (dic.ContainsKey("{isRangeMode}"))
                    dic.Remove("{isRangeMode}");
                var isRadiusConst = dic.ContainsKey("{isConstMode}") && Convert.ToBoolean(dic["{isConstMode}"]);
                if (dic.ContainsKey("{isConstMode}"))
                    dic.Remove("{isConstMode}");
                var isRadiusRegion = dic.ContainsKey("{isRegionMode}") && Convert.ToBoolean(dic["{isRegionMode}"]);
                if (dic.ContainsKey("{isRegionMode}"))
                    dic.Remove("{isRegionMode}");
                if(!dic.ContainsKey("{NewLine}"))
                    dic.Add("{NewLine}", Environment.NewLine);

                var lines = (await File.ReadAllLinesAsync(fileName))
                    .Where(a => !a.StartsWith("//") && !string.IsNullOrWhiteSpace(a)).ToList();

                foreach (var pair in dic)
                {
                    for (var i = 0; i < lines.Count; i++)
                    {
                        lines[i] = lines[i].Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase).Trim().TrimStart('\t');
                    }
                }

                bool isAuthor = false;
                var embed = new EmbedBuilder();
                EmbedAuthorBuilder author = null;

                lines.ForEach(line =>
                {
                    var text = line;
                    //var lowerText = text.ToLower();

                    if (text.StartsWith("EmbedBuilder", StringComparison.OrdinalIgnoreCase)) return;

                    if (text.StartsWith("#if {isRangeMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isRadiusRange) return;
                        text = text.Substring(17).Trim();
                    }
                    if (text.StartsWith("#if !{isRangeMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isRadiusRange) return;
                        text = text.Substring(18).Trim();
                    }
                    if (text.StartsWith("#if {isConstMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isRadiusConst) return;
                        text = text.Substring(17).Trim();
                    }
                    if (text.StartsWith("#if !{isConstMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isRadiusConst) return;
                        text = text.Substring(18).Trim();
                    }

                    if (text.StartsWith("#if {isRegionMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isRadiusRegion) return;
                        text = text.Substring(18).Trim();
                    }
                    if (text.StartsWith("#if !{isRegionMode}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isRadiusRegion) return;
                        text = text.Substring(19).Trim();
                    }

                    if (text.StartsWith("#if {isNpcKill}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isNpcKill) return;
                        text = text.Substring(15).Trim();
                    }
                    if (text.StartsWith("#if !{isNpcKill}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isNpcKill) return;
                        text = text.Substring(16).Trim();
                    }

                    if (text.StartsWith("#if {isLoss}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isLoss) return;
                        text = text.Substring(12).Trim();
                    }
                    if (text.StartsWith("#if !{isLoss}", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isLoss) return;
                        text = text.Substring(13).Trim();
                    }
                            
                    if (text.StartsWith("#if {isAwoxKill}", StringComparison.OrdinalIgnoreCase)) {
                        if (!isAwox) return;
                        text = text.Substring(16).Trim();
                    }
                    if (text.StartsWith( "#if !{isAwoxKill}", StringComparison.OrdinalIgnoreCase )) {
                        if (isAwox) return;
                        text = text.Substring(17).Trim();
                    }
                            
                    if (text.StartsWith( "#if {isSoloKill}", StringComparison.OrdinalIgnoreCase )) {
                        if (!isSolo) return;
                        text = text.Substring(16).Trim();
                    }
                    if (text.StartsWith( "#if !{isSoloKill}", StringComparison.OrdinalIgnoreCase )) {
                        if (isSolo) return;
                        text = text.Substring(17).Trim();
                    }

                    if (text.StartsWith("#var", StringComparison.OrdinalIgnoreCase))
                    {
                        var data = text.Substring(4).Trim().Split(" ");
                        if(data.Length < 2)
                            throw new Exception($"Invalid #var declaration in template file {fileName}");
                        var name = data[0];
                        var value = string.Join(' ', data).Trim().Substring(name.Length + 1);
                       // LogHelper.Log($"{name} : {value}", LogSeverity.Info, LogCat.Templates, true, true).GetAwaiter().GetResult();
                        vars.Add(name, value);
                        return;
                    }

                    if(text.Contains("{var:", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = text.GetUntilOrEmpty(text.IndexOf("{var:", StringComparison.OrdinalIgnoreCase)+ 5, "}");
                        var v = vars[name];
                        text = text.Replace($"{{var:{name}}}", v);
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
                            embed.AddField(result[0], result[1], true);
                        }
                        else if (text.StartsWith(".WithFooter", StringComparison.OrdinalIgnoreCase))
                            embed.WithFooter(GetText(text));
                        else if (text.StartsWith(".WithTimestamp", StringComparison.OrdinalIgnoreCase))
                        {
                            if (DateTimeOffset.TryParse(dic["{timestamp}"], out var time))
                                    embed.WithTimestamp(time);
                            else embed.WithCurrentTimestamp();
                        }
                    }
                });
                return embed.Build();

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Templates);
                return null;
            }
        }

        public static uint GetColor(string text)
        {
            var index = text.IndexOf('(');
            var tex = text.Substring(index + 1, text.Length - index - 2);
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
