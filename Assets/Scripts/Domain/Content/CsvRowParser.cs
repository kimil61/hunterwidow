using System.Collections.Generic;
using System.Text;

namespace HunterWidow.Domain.Content
{
    /// <summary>
    /// Reads one RFC 4180-style CSV row without depending on Unity or a locale.
    /// Locale values may contain commas and escaped quotation marks, while keys
    /// remain ordinary strings.
    /// </summary>
    public static class CsvRowParser
    {
        public static string[] Parse(string row)
        {
            var values = new List<string>();
            var value = new StringBuilder();
            var inQuotes = false;
            var source = row ?? string.Empty;

            for (var characterIndex = 0; characterIndex < source.Length; characterIndex++)
            {
                var character = source[characterIndex];
                if (character == '"')
                {
                    if (inQuotes && characterIndex + 1 < source.Length && source[characterIndex + 1] == '"')
                    {
                        value.Append(character);
                        characterIndex++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    values.Add(value.ToString());
                    value.Length = 0;
                    continue;
                }

                value.Append(character);
            }

            values.Add(value.ToString());
            return values.ToArray();
        }
    }
}
