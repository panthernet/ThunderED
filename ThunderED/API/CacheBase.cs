using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        /// <param name="type">Cahce type</param>
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

        protected async Task<T> GetFromDbCache<T>(object id, int days)
            where T: class
        {
            var data = await SQLHelper.SelectCache<T>(id, days);
            if (data == null) return null;
            await SQLHelper.SetCacheLastAccess(id, typeof(T).Name);
            return data;
        }

        protected async Task UpdateDbCache<T>(T data, object id, int days) 
            where T : class
        {
            await SQLHelper.UpdateCache(data, id, days);
        }

        protected async Task RemoveDbCache(object id, object value) 
        {
            await SQLHelper.DeleteCache(id, value);
        }

        #endregion
    }
}
