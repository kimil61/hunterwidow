using System;
using System.Collections.Generic;
using System.IO;

namespace HunterWidow.Domain.Content
{
    public sealed class ValidationIssue
    {
        public ValidationIssue(string code, string file, int line, string message)
        {
            Code = code;
            File = file;
            Line = line;
            Message = message;
        }

        public string Code { get; }

        public string File { get; }

        public int Line { get; }

        public string Message { get; }

        public override string ToString()
        {
            return File + ":" + Line + " [" + Code + "] " + Message;
        }
    }

    public sealed class ContentValidationReport
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues => issues;

        public bool HasErrors => issues.Count > 0;

        public void Add(string code, string file, int line, string message)
        {
            issues.Add(new ValidationIssue(code, file, Math.Max(1, line), message));
        }

        public string ToMultilineText()
        {
            if (issues.Count == 0)
            {
                return "Content validation passed.";
            }

            var lines = new string[issues.Count];
            for (var issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                lines[issueIndex] = issues[issueIndex].ToString();
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public sealed class ContentItem
    {
        private readonly IDictionary<string, object> fields;

        internal ContentItem(string id, string sourceFile, int sourceLine, IDictionary<string, object> fields)
        {
            Id = id;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
            this.fields = fields;
        }

        public string Id { get; }

        public string SourceFile { get; }

        public int SourceLine { get; }

        public IReadOnlyDictionary<string, object> Fields => (IReadOnlyDictionary<string, object>)fields;

        public string Type => ContentValues.GetString(fields, "type");

        public string GetString(string key, string fallback = "")
        {
            return ContentValues.GetString(fields, key, fallback);
        }

        public double GetNumber(string key, double fallback = 0d)
        {
            return ContentValues.GetNumber(fields, key, fallback);
        }

        public bool GetBool(string key, bool fallback = false)
        {
            return ContentValues.GetBool(fields, key, fallback);
        }

        public IDictionary<string, object> GetObject(string key)
        {
            return ContentValues.GetObject(fields, key);
        }

        public IList<object> GetArray(string key)
        {
            return ContentValues.GetArray(fields, key);
        }
    }

    public sealed class ContentDatabase
    {
        private readonly Dictionary<string, ContentItem> byId = new Dictionary<string, ContentItem>(StringComparer.Ordinal);
        private readonly List<ContentItem> allItems = new List<ContentItem>();

        public IReadOnlyList<ContentItem> AllItems => allItems;

        internal void Add(ContentItem item)
        {
            allItems.Add(item);
            if (!string.IsNullOrEmpty(item.Id) && !byId.ContainsKey(item.Id))
            {
                byId.Add(item.Id, item);
            }
        }

        public bool TryGet(string id, out ContentItem item)
        {
            return byId.TryGetValue(id, out item);
        }

        public IEnumerable<ContentItem> FindByType(string type)
        {
            for (var itemIndex = 0; itemIndex < allItems.Count; itemIndex++)
            {
                var item = allItems[itemIndex];
                if (string.Equals(item.Type, type, StringComparison.Ordinal))
                {
                    yield return item;
                }
            }
        }
    }

    public sealed class ContentLoadResult
    {
        internal ContentLoadResult(string rootPath, ContentDatabase database, ContentValidationReport report)
        {
            RootPath = rootPath;
            Database = database;
            Report = report;
        }

        public string RootPath { get; }

        public ContentDatabase Database { get; }

        public ContentValidationReport Report { get; }

        public bool HasContent => Database.AllItems.Count > 0;
    }

    public sealed class ContentBootStatus
    {
        private ContentBootStatus(bool canStart, string messageCode)
        {
            CanStart = canStart;
            MessageCode = messageCode;
        }

        public bool CanStart { get; }

        public string MessageCode { get; }

        public static ContentBootStatus From(ContentLoadResult result)
        {
            if (result == null || !result.HasContent || result.Report.HasErrors)
            {
                return new ContentBootStatus(false, "content_missing");
            }

            return new ContentBootStatus(true, "content_ready");
        }
    }

    public static class ContentValues
    {
        public static string GetString(IDictionary<string, object> values, string key, string fallback = "")
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value is string
                ? (string)value
                : fallback;
        }

        public static double GetNumber(IDictionary<string, object> values, string key, double fallback = 0d)
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value is double
                ? (double)value
                : fallback;
        }

        public static bool GetBool(IDictionary<string, object> values, string key, bool fallback = false)
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value is bool
                ? (bool)value
                : fallback;
        }

        public static IDictionary<string, object> GetObject(IDictionary<string, object> values, string key)
        {
            object value;
            return values != null && values.TryGetValue(key, out value)
                ? value as IDictionary<string, object>
                : null;
        }

        public static IList<object> GetArray(IDictionary<string, object> values, string key)
        {
            object value;
            return values != null && values.TryGetValue(key, out value)
                ? value as IList<object>
                : null;
        }

        public static IDictionary<string, object> AsObject(object value)
        {
            return value as IDictionary<string, object>;
        }

        public static IList<object> AsArray(object value)
        {
            return value as IList<object>;
        }
    }

    public static class ContentLoader
    {
        public static ContentLoadResult Load(string rootPath)
        {
            var report = new ContentValidationReport();
            var database = new ContentDatabase();

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                report.Add("CONTENT_DIRECTORY_MISSING", "content", 1, "Content directory does not exist.");
                return new ContentLoadResult(rootPath ?? string.Empty, database, report);
            }

            var normalizedRootPath = Path.GetFullPath(rootPath);
            var jsonFiles = Directory.GetFiles(normalizedRootPath, "*.json", SearchOption.AllDirectories);
            Array.Sort(jsonFiles, StringComparer.Ordinal);

            if (jsonFiles.Length == 0)
            {
                report.Add("CONTENT_EMPTY", "content", 1, "No JSON content files were found.");
                return new ContentLoadResult(normalizedRootPath, database, report);
            }

            var foundPack = false;
            for (var fileIndex = 0; fileIndex < jsonFiles.Length; fileIndex++)
            {
                var absolutePath = jsonFiles[fileIndex];
                var relativePath = MakeRelativePath(normalizedRootPath, absolutePath);
                if (string.Equals(relativePath, "pack.json", StringComparison.OrdinalIgnoreCase))
                {
                    foundPack = true;
                }

                ReadFile(normalizedRootPath, absolutePath, relativePath, database, report);
            }

            if (!foundPack)
            {
                report.Add("PACK_MANIFEST_MISSING", "content", 1, "pack.json is required at the root of a content pack.");
            }

            ContentValidator.Validate(normalizedRootPath, database, report);
            return new ContentLoadResult(normalizedRootPath, database, report);
        }

        private static void ReadFile(
            string rootPath,
            string absolutePath,
            string relativePath,
            ContentDatabase database,
            ContentValidationReport report)
        {
            string text;
            try
            {
                text = File.ReadAllText(absolutePath);
            }
            catch (Exception exception)
            {
                report.Add("CONTENT_READ_FAILED", relativePath, 1, exception.Message);
                return;
            }

            IDictionary<string, object> document;
            try
            {
                document = ContentValues.AsObject(MiniJson.Parse(text));
            }
            catch (JsonParseException exception)
            {
                report.Add("JSON_SYNTAX", relativePath, exception.Line, exception.Message);
                return;
            }

            if (document == null)
            {
                report.Add("ROOT_OBJECT_REQUIRED", relativePath, 1, "The root JSON value must be an object.");
                return;
            }

            object schemaVersion;
            if (!document.TryGetValue("schemaVersion", out schemaVersion) || !(schemaVersion is double))
            {
                report.Add("SCHEMA_VERSION_REQUIRED", relativePath, 1, "Every content file requires numeric schemaVersion.");
            }
            else if ((double)schemaVersion != 1d)
            {
                report.Add("SCHEMA_VERSION_UNSUPPORTED", relativePath, 1, "Only schemaVersion 1 is supported.");
            }

            object rawItems;
            if (!document.TryGetValue("items", out rawItems))
            {
                report.Add("ITEMS_REQUIRED", relativePath, 1, "Every content file requires an items array.");
                return;
            }

            var items = ContentValues.AsArray(rawItems);
            if (items == null)
            {
                report.Add("ITEMS_ARRAY_REQUIRED", relativePath, 1, "items must be an array.");
                return;
            }

            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var fields = ContentValues.AsObject(items[itemIndex]);
                if (fields == null)
                {
                    report.Add("ITEM_OBJECT_REQUIRED", relativePath, FindLine(text, "items"), "Each item must be an object.");
                    continue;
                }

                var id = ContentValues.GetString(fields, "id");
                var itemLine = FindLine(text, string.IsNullOrEmpty(id) ? "items" : id);
                database.Add(new ContentItem(id, relativePath, itemLine, fields));
            }
        }

        internal static int FindLine(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
            {
                return 1;
            }

            var index = text.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                return 1;
            }

            var line = 1;
            for (var characterIndex = 0; characterIndex < index; characterIndex++)
            {
                if (text[characterIndex] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        internal static string MakeRelativePath(string rootPath, string absolutePath)
        {
            var rootWithSeparator = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var relative = absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                ? absolutePath.Substring(rootWithSeparator.Length)
                : absolutePath;
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }
}
