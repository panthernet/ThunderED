using System.Collections;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace TED_ConfigEditor.Classes
{
    public abstract class ValidatableSettings: IValidatable
    {
        [JsonIgnore]
        public abstract string this[string columnName] { get; }

        [JsonIgnore]
        public string Error => "!!!!";

        public string Validate(bool sub = false)
        {
            var sb = new StringBuilder();
            GetType().GetProperties().Where(a=> a.CanRead && a.CanWrite).ToList().ForEach(property =>
            {
                var val = this[property.Name];
                if (!string.IsNullOrEmpty(val))
                {
                    sb.Append(val);
                    sb.Append("\n");
                }

                if (typeof(IDictionary).IsAssignableFrom(property.PropertyType))
                {
                    var valueType = property.PropertyType.GenericTypeArguments.Last();
                    if(!typeof(IValidatable).IsAssignableFrom(valueType)) return;

                    var parentProp = property.GetValue(this) as IDictionary;
                    foreach (DictionaryEntry kvp in parentProp)
                    {
                        var res = ((IValidatable) kvp.Value).Validate(true);
                        if (!string.IsNullOrEmpty(res))
                        {
                            if(!sub)
                                sb.AppendLine($"  Group: {kvp.Key}");
                            else sb.AppendLine($"      Group: {kvp.Key}");
                            sb.AppendLine(res);
                        }
                    }
                }
            });
            if (sb.Length > 0)
            {
                var txt = sub ? "    Subclass" : "Class";
                sb.Insert(0, $"{txt}: {GetType().Name}\n");
            }

            return sb.ToString();
        }

        protected string Compose(string property, string message)
        {
            return $"--> {property}: {message}";
        }
    }
}
