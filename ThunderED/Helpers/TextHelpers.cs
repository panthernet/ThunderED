using System;

namespace ThunderED
{
    public static class TextHelpers
    {
        public static string GetUntilOrEmpty(this string text, int startIndex = 0, string stopAt = "-", bool includeLast = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var charLocation = text.IndexOf(stopAt, startIndex, StringComparison.Ordinal);// - (includeLast ? 0 : 1);
            return charLocation > 0 ? text.Substring(startIndex, charLocation- startIndex) : String.Empty;
        }

        public static string RemoveLocalizedTag(this string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] !='<') return value;
            var index1 = value.IndexOf('>');
            if (index1 == -1) return value;
            var index2 = value.IndexOf('<', index1);
            if (index2 == -1) return value;
            return value.Substring(index1+1, index2 - index1 - 1).TrimEnd('*');
        }
    }
}
