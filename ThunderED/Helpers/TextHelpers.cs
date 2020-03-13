using System;

namespace ThunderED.Helpers
{
    public static class TextHelpers
    {
        public static string GetUntilOrEmpty(this string text, int startIndex = 0, string stopAt = "-", bool includeLast = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var charLocation = text.IndexOf(stopAt, startIndex, StringComparison.Ordinal);// - (includeLast ? 0 : 1);
            return charLocation > 0 ? text.Substring(startIndex, charLocation- startIndex) : String.Empty;
        }
    }
}
