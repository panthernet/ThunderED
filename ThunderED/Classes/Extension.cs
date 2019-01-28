using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThunderED.Classes
{
    public static class Extension
    {
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
    }

}
