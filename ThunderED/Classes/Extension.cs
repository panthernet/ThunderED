using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThunderED.Classes
{
    public static class Extension
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static void AddOnlyNew<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, TValue value)
        {
            if (!dic.ContainsKey(key))
                dic.Add(key, value);
        }

        public static IEnumerable<T> TakeSmart<T>(this IEnumerable<T> list, int count)
        {
            if (list == null) return null;
            return list.Count() > count ? list.Take(count): list;
        }

        public static IEnumerable<string> Split(this string str, int chunkSize)
        {
            IEnumerable<string> retVal = Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));

            if (str.Length % chunkSize > 0)
                retVal = retVal.Append(str.Substring(str.Length / chunkSize * chunkSize, str.Length % chunkSize));

            return retVal;
        }

        public static void AddOrUpdateEx<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, TValue value)
        {
            if (dic.ContainsKey(key))
                dic[key] = value;
            else dic.Add(key, value);
        }

        public static TValue GetOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
            where TValue: class
        {
            return dic.ContainsKey(key) ? dic[key] : null;
        }

        public static string FixedLength(this string value, int length)
        {
            if (string.IsNullOrEmpty(value)) return FillSpaces("", length);
            return length < value.Length ? value.Substring(0, length) : (value.Length < length ? FillSpaces(value, length) : value);
        }

        private static string FillSpaces(this string value, int length)
        {
            if (value.Length >= length) return value;
            var sb = new StringBuilder();
            sb.Append(value);
            for (int i = value.Length; i < length; i++)
                sb.Append(" ");
            return sb.ToString();
        }
    }

}
