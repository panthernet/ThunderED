using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ThunderED.Json.Converters
{
    class KVConverter<TKey, TValue> : JsonConverter where TValue : new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = value as List<KeyValuePair<TKey, TValue>>;
            writer.WriteStartArray();
            foreach (var item in list)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(item.Key?.ToString() ?? "");
                writer.WriteValue(item.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            var output = new List<KeyValuePair<TKey, TValue>>();
            foreach (var v in jsonObject.Children())
            {
                var key = v.Path;

                var result = new TValue();
                serializer.Populate(v.First.CreateReader(), result);
                output.Add(new KeyValuePair<TKey, TValue>((TKey) (object) key, result));
            }

            return output;
        }

        private object Create(Type objectType, JObject jsonObject)
        {
            if (FieldExists("Key", jsonObject))
            {
                return jsonObject["Key"].ToString();
            }

            if (FieldExists("Value", jsonObject))
            {
                return jsonObject["Value"].ToString();
            }
            return null;
        }

        private bool FieldExists(string fieldName, JObject jsonObject)
        {
            return jsonObject[fieldName] != null;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<KeyValuePair<TKey, TValue>>);
        }
    }
}
