using System;
using HunterWidow.Domain.Content;

namespace HunterWidow.Tools.ContentValidator
{
    internal static class Program
    {
        private static int Main(string[] arguments)
        {
            var contentPath = arguments.Length > 0 ? arguments[0] : ".";
            var result = ContentLoader.Load(contentPath);
            Console.WriteLine(result.Report.ToMultilineText());
            Console.WriteLine("Validated " + result.Database.AllItems.Count + " item(s).");
            return result.Report.HasErrors ? 1 : 0;
        }
    }
}
