using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThunderED.Classes
{
    public class OrderedDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _map = new();
        private readonly List<TKey> _list = new();

        public void Add(TKey key, TValue value) {
            if (!_map.ContainsKey(key)) {
                _list.Add(key);
            }
            _map[key] = value;
        }

        public void Add(TKey key, TValue value, int index) {
            if (_map.ContainsKey(key)) {
                _list.Remove(key);
            }
            _map[key] = value;
            _list.Insert(index, key);
        }

        public TValue GetValue(TKey key) {
            return _map[key];
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetItems() {
            foreach (var key in _list) { 
                var value = _map[key];
                yield return new KeyValuePair<TKey, TValue>(key, value);
            }
        }
    }
}
