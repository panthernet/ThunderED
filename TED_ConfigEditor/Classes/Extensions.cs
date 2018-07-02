using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TED_ConfigEditor.Classes
{
    public static class Extensions
    {
        public const string ERR_MSG_VALUEEMPTY = "Value must not be empty!";

        private static readonly Regex AllNumsRegex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
        private static readonly Regex NumsRegex = new Regex("[^0-9-]+"); //regex that matches disallowed text
        public static bool IsNumericValue(string text)
        {
            return !NumsRegex.IsMatch(text);
        }

        public static bool IsAllNumericValue(string text)
        {
            return !NumsRegex.IsMatch(text);
        }

        public static bool IsKeyValuePair(this object value)
        {
            if (value != null)
            {
                var valueType = value.GetType();
                if (valueType.IsGenericType)
                {
                    var baseType = valueType.GetGenericTypeDefinition();
                    if (baseType == typeof(KeyValuePair<,>))
                    {
                        //Type[] argTypes = baseType.GetGenericArguments();
                        // now process the values
                        return true;
                    }
                }
            }

            return false;
        }

        public static object GetAttributeValue<T>(this PropertyInfo obj, string propertyName)
        {
            var attr = obj.GetCustomAttributes(typeof(T), false).FirstOrDefault();
            return attr == null ? null : typeof(T).GetProperty(propertyName)?.GetValue(attr);

        }

        public static bool HasAttribute<T>(this PropertyInfo obj)
        {
            return obj.GetCustomAttributes(typeof(T), false).FirstOrDefault() != null;
        }

        public static bool IsKeyValuePair(this Type valueType)
        {
            if (valueType != null)
            {
                if (valueType.IsGenericType)
                {
                    var baseType = valueType.GetGenericTypeDefinition();
                    if (baseType == typeof(KeyValuePair<,>))
                    {
                        //Type[] argTypes = baseType.GetGenericArguments();
                        // now process the values
                        return true;
                    }
                }
            }

            return false;
        }

        public static object GetValueFromPair(this object value)
        {
            return value.GetType().GetProperty("Value").GetValue(value);
        }

        public static object GetKeyFromPair(this object value)
        {
            return value.GetType().GetProperty("Key").GetValue(value);
        }

        public static Type ExtractValueTypeFromPair(this Type valueType)
        {
            return valueType.GenericTypeArguments.Last();
        }

        public static string DescriptionAttr<T>(this T source)
        {
            FieldInfo fi = source.GetType().GetField(source.ToString());

            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(
                typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0) return attributes[0].Description;
            else return source.ToString();
        }

        public static void SetPropertyValueFromString(this object target,               
            string propertyName, string propertyValue)
        {
            PropertyInfo oProp = target.GetType().GetProperty(propertyName);
            Type tProp = oProp.PropertyType;

            //Nullable properties have to be treated differently, since we 
            //  use their underlying property to set the value in the object
            if (tProp.IsGenericType
                && tProp.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                //if it's null, just set the value from the reserved word null, and return
                if (propertyValue == null)
                {
                    oProp.SetValue(target, null, null);
                    return;
                }

                //Get the underlying type property instead of the nullable generic
                tProp = new NullableConverter(oProp.PropertyType).UnderlyingType;
            }

            //use the converter to get the correct value
            oProp.SetValue(target, Convert.ChangeType(propertyValue, tProp), null);
        }
    }
}
