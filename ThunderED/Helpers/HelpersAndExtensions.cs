using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ThunderED.Classes;

namespace ThunderED.Helpers
{
    public static class HelpersAndExtensions
    {
        public static string ToJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T FromJson<T>(this T obj, string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string ToPercent(this float value)
        {
            return $"{value * 100}%";
        }

        public static void AddOrUpdate<T, T2>(this ConcurrentDictionary<T, T2> dic, T id, T2 data)
        {
            if (dic.ContainsKey(id))
                dic[id] = data;
            else dic.TryAdd(id, data);
        }

        public static T2 Get<T, T2>(this ConcurrentDictionary<T, T2> dic, T id)
            where T2: class
        {
            return dic.ContainsKey(id) ? dic[id] : null;
        }

        public static void Remove<T, T2>(this ConcurrentDictionary<T, T2> dic, T id)
            where T2: class
        {
            dic.TryRemove(id, out var _);
        }

        public static LogSeverity ToSeverity(this string str)
        {
            switch (str.ToLower())
            {
                case "info":
                    return LogSeverity.Info;
                case "debug":
                    return LogSeverity.Debug;
                case "warning":
                    return LogSeverity.Warning;
                case "critical":
                    return LogSeverity.Critical;
                case "error":
                    return LogSeverity.Error;
                case "verbose":
                    return LogSeverity.Verbose;
                default:
                    return LogSeverity.Info;
            }
        }

        public static LogSeverity ToSeverity(this Discord.LogSeverity severity)
        {
            switch (severity)
            {
                case Discord.LogSeverity.Info:
                    return LogSeverity.Info;
                case Discord.LogSeverity.Debug:
                    return LogSeverity.Debug;
                case Discord.LogSeverity.Warning:
                    return LogSeverity.Warning;
                case Discord.LogSeverity.Critical:
                    return LogSeverity.Critical;
                case Discord.LogSeverity.Error:
                    return LogSeverity.Error;
                case Discord.LogSeverity.Verbose:
                    return LogSeverity.Verbose;
                default:
                    return LogSeverity.Info;
            }
        }

        public static string ToFormattedString(this TimeSpan ts)
        {
            const string separator = ", ";

            if (ts.Milliseconds < 1) { return "No time"; }

            return string.Join(separator, new string[]
            {
                ts.Days > 0 ? $"{ts.Days}d " : null,
                ts.Hours > 0 ? $"{ts.Hours}h " : null,
                ts.Minutes > 0 ? $"{ts.Minutes}m" : null
                //ts.Seconds > 0 ? ts.Seconds + (ts.Seconds > 1 ? " seconds" : " second") : null,
                //ts.Milliseconds > 0 ? ts.Milliseconds + (ts.Milliseconds > 1 ? " milliseconds" : " millisecond") : null,
            }.Where(t => t != null));
        }

        internal static Dictionary<string, string> ParseNotificationText(string text)
        {
            var dic = new Dictionary<string, string>();
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList().ForEach(a =>
            {
                var res = a.Split(':');
                if (res.Length == 1)
                {
                    if(!dic.ContainsKey(res[0]))
                        dic.Add(res[0], null);
                }
                else{
                    var value = res[1].Trim();
                    value = value.StartsWith("&id") ? value.Split(' ')[1] : value;
                    value = value?.Trim();
                    var key = res[0]?.Trim();
                    if(!dic.ContainsKey(key))
                        dic.Add(key, value == "null" ? null : value);
                }
            });
            return dic;
        }

	
        internal static string GenerateUnicodePercentage(double percentage)
        {
            string styles = "░▒▓█";

            double d, full, middle, rest, x, min_delta = double.PositiveInfinity;
            char full_symbol = styles[styles.Length - 1], m;
            var n = styles.Length;
            var max_size = 20;
            var min_size = 20;

            var i = max_size;

            string String = "";
            if (percentage == 100)
            {
                return repeat(full_symbol, 10);
            }
            else
            {
                percentage = percentage / 100;

                while (i > 0 && i >= min_size)
                {

                    x = percentage * i;
                    full = Math.Floor(x);
                    rest = x - full;
                    middle = Math.Floor(rest * n);

                    if (percentage != 0 && full == 0 && middle == 0) middle = 1;

                    d = Math.Abs(percentage - (full + middle / n) / i) * 100;

                    if (d < min_delta)
                    {
                        min_delta = d;

                        m = styles[(int)middle];
                        if (full == i) m = ' ';
                        String = repeat(full_symbol, full) + m + repeat(styles[0], i - full - 1);
                    }
                    i--;
                }
            }

            return String;
        }

        static string repeat(char s, double i)
        {
            var r = "";
            for (var j = 0; j < i; j++) r += s;
            return r;
        }
    }
}
