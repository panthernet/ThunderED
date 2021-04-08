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

        public static IEnumerable<List<T>> SplitList<T>(this List<T> locations, int nSize = 30)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }



        public static void AddOrUpdate<T, T2>(this ConcurrentDictionary<T, T2> dic, T id, T2 data)
        {
            if (dic.ContainsKey(id))
                dic[id] = data;
            else dic.TryAdd(id, data);
        }

        public static string GetRemains(this DateTime? entry, string template)
        {
            if (!entry.HasValue) return null;
            var dif = (entry.Value - DateTime.UtcNow);
            return string.Format(template, dif.Days, dif.Hours, dif.Minutes);
        }

        public static string GetRemains(this DateTime entry, string template)
        {
            var dif = (entry - DateTime.UtcNow);
            return string.Format(template, dif.Days < 0 ? 0 : dif.Days, dif.Hours < 0 ? 0 : dif.Hours, dif.Minutes < 0 ? 0 : dif.Minutes);
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



        public static Dictionary<string, string> ParseNotificationText(string text)
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


        public static string GenerateUnicodePercentage(double percentage)
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

        public static string RemoveDotValue(this string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return value;
                var index = value.IndexOf(".", StringComparison.Ordinal);
                return index < 0 ? value : value.Substring(0, index);
            }
            catch
            {
                return null;
            }
        }

        public static T Clone<T>(this T source)
        {
            var serialized = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<T>(serialized);
        }

        public static void ReplaceItem<T>(this List<T> list, T value)
            where T : IIdentifiable
        {
            var old = list.FirstOrDefault(a => a.Id == value.Id);
            if (old != null)
            {
                var index = list.IndexOf(old);
                list.Remove(old);
                list.Insert(index, value);
            }
            else list.Add(value);
        }

        public static DateTime ToEveTime(this string value)
        {
            return DateTime.TryParse(value, out var localTimestamp) ? localTimestamp.ToUniversalTime() : DateTime.MinValue;
        }

        public static string ToEveTimeString(this string value)
        {
            var t = value.ToEveTime();
            return $"{t.ToShortDateString()} {t.ToShortTimeString()}";
        }
    }
}
