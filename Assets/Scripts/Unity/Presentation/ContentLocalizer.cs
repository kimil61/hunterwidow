using System;
using System.Collections.Generic;
using System.IO;
using HunterWidow.Domain.Content;
using UnityEngine;

namespace HunterWidow.Unity.Presentation
{
    public sealed class ContentLocalizer
    {
        private readonly Dictionary<string, string[]> values = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> languageColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> warnedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly string defaultLanguageCode;
        private int defaultLanguageColumn = 1;
        private int languageColumn = 1;

        public ContentLocalizer(string csvPath, string defaultLocale = "ko")
        {
            defaultLanguageCode = string.IsNullOrWhiteSpace(defaultLocale) ? "ko" : defaultLocale;
            LanguageCode = defaultLanguageCode;
            if (!File.Exists(csvPath))
            {
                Warn("locale.file", "Locale file is missing: " + csvPath);
                return;
            }

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0)
            {
                Warn("locale.empty", "Locale file has no header: " + csvPath);
                return;
            }

            var headings = CsvRowParser.Parse(lines[0]);
            for (var columnIndex = 1; columnIndex < headings.Length; columnIndex++)
            {
                var languageCode = headings[columnIndex].Trim();
                if (!string.IsNullOrEmpty(languageCode))
                {
                    languageColumns[languageCode] = columnIndex;
                }
            }

            int configuredDefaultColumn;
            if (languageColumns.TryGetValue(defaultLanguageCode, out configuredDefaultColumn))
            {
                defaultLanguageColumn = configuredDefaultColumn;
                languageColumn = configuredDefaultColumn;
            }
            else
            {
                Warn("locale.default", "Default locale column is missing: " + defaultLanguageCode);
            }

            for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var columns = CsvRowParser.Parse(lines[lineIndex]);
                if (columns.Length > 1 && !string.IsNullOrWhiteSpace(columns[0]))
                {
                    values[columns[0]] = columns;
                }
            }
        }

        public string LanguageCode { get; private set; }

        public void SetLanguage(string languageCode)
        {
            LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? defaultLanguageCode : languageCode;
            int requestedColumn;
            if (languageColumns.TryGetValue(LanguageCode, out requestedColumn))
            {
                languageColumn = requestedColumn;
                return;
            }

            languageColumn = defaultLanguageColumn;
            Warn("locale.language." + LanguageCode, "Locale is unavailable; using default: " + LanguageCode);
        }

        public string Get(string key)
        {
            string[] columns;
            if (!values.TryGetValue(key, out columns))
            {
                Warn("locale.key." + key, "Locale key is missing: " + key);
                return key;
            }

            if (languageColumn < columns.Length && !string.IsNullOrEmpty(columns[languageColumn]))
            {
                return columns[languageColumn];
            }

            Warn("locale.value." + key + "." + LanguageCode, "Locale value is missing; using default: " + key);
            return defaultLanguageColumn < columns.Length && !string.IsNullOrEmpty(columns[defaultLanguageColumn])
                ? columns[defaultLanguageColumn]
                : key;
        }

        private void Warn(string warningKey, string message)
        {
            if (warnedKeys.Add(warningKey))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
