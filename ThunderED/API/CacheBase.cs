using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Newtonsoft.Json;

using ThunderED.Helpers;

namespace ThunderED.API
{
    /// <summary>
    /// Base class for API caching
    /// </summary>
    public abstract class CacheBase
    {
        /// <summary>
        /// Purge all outdated cache
        /// </summary>
        internal virtual void PurgeCache()
        {
        }

        /// <summary>
        /// Clear all cache by type. Everything if null.
        /// </summary>
        /// <param name="type">Cache type</param>
        internal virtual void ResetCache(string type = null)
        {
        }

        #region Memory cache

        protected class CacheEntry<T>
        {
            public string Id;
            public string EntityType;
            public DateTime LastUpdate = DateTime.Now;
            public DateTime LastAccess = DateTime.Now;
            public T Entry;

            public CacheEntry(T charData)
            {
                Entry = charData;
                EntityType = typeof(T).Name;
            }
        }

        protected T GetFromCache<T>(ConcurrentDictionary<object, CacheEntry<T>> cache, object id, out DateTime lastUpdate)
            where T: class
        {
            var result = cache.Get(id);
            lastUpdate = DateTime.Now;
            if(result != null)
            {
                result.LastAccess = DateTime.Now;
                lastUpdate = result.LastUpdate;
            }
            return result?.Entry;
        }

        protected void UpdateCache<T>(ConcurrentDictionary<object, CacheEntry<T>> dic, T charData, object id)
        {
            CacheEntry<T> entry;
            if (dic.ContainsKey(id))
            {
                entry = dic[id];
                entry.Entry = charData;
                entry.LastAccess = entry.LastUpdate = DateTime.Now;
            }
            else
            {            
                entry = new CacheEntry<T>(charData);
            }
            dic.AddOrUpdate(id, entry);              
        }
        #endregion

        #region Database cache

        protected async Task<T> GetFromDbCache<T>(string id, int days)
            where T: class
        {
            var data = await DbHelper.GetCache<T>(id, days);
            if (data == null) return null;
            await DbHelper.SetCacheLastAccess(id, typeof(T));
            return data;
        }

        protected async Task UpdateDbCache<T>(T data, string id, int days) 
            where T : class
        {
            await DbHelper.UpdateCache(id, data, days);
        }

        protected async Task RemoveDbCache(string type, string id) 
        {
            await DbHelper.DeleteCache(id, type);
        }

        #endregion
    }
}
