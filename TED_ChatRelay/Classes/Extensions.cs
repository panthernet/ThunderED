using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThunderED.Classes
{
    public static class Extensions
    {
        public static IEnumerable<string> SplitToLines(this string stringToSplit, int maxLineLength, string delimiter = " ", bool preserveDelimiter = false)
        {
            var words = stringToSplit.Split(delimiter);
            var line = new StringBuilder();
            var lastOne = words.LastOrDefault();
            foreach (var word in words)
            {
                if (word.Length + line.Length <= maxLineLength)
                {
                    line.Append(word + delimiter);
                }
                else
                {
                    if (line.Length > 0)
                    {
                        var res2 = line.ToString();
                        if ((!preserveDelimiter || lastOne == word) && res2.EndsWith(delimiter))
                            res2 = res2.Substring(0, res2.Length - delimiter.Length);
                        yield return res2;
                        line.Clear();
                    }
                    var overflow = word;
                    while (overflow.Length > maxLineLength)
                    {
                        yield return overflow.Substring(0, maxLineLength);
                        overflow = overflow.Substring(maxLineLength);
                    }
                    line.Append(overflow + delimiter);
                }
            }

            var res = line.ToString();
            if (res.EndsWith(delimiter))
                res = res.Substring(0, res.Length - delimiter.Length);
            yield return res;
        }

        public static IEnumerable<string> SplitBy(this string str, int chunkSize, bool remainingInFront = false)
        {
            var count = (int) Math.Ceiling(str.Length/(double) chunkSize);
            int Start(int index) => remainingInFront ? str.Length - (count - index) * chunkSize : index * chunkSize;
            int End(int index) => Math.Min(str.Length - Math.Max(Start(index), 0), Math.Min(Start(index) + chunkSize - Math.Max(Start(index), 0), chunkSize));
            return Enumerable.Range(0, count).Select(i => str.Substring(Math.Max(Start(i), 0),End(i)));
        }
    }
}
