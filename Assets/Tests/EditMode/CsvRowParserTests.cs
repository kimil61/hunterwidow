using HunterWidow.Domain.Content;
using NUnit.Framework;

namespace HunterWidow.Tests
{
    [TestFixture]
    public sealed class CsvRowParserTests
    {
        [Test]
        public void LocaleCsvRowsKeepQuotedCommasAndEscapedQuotesInTheirOwnColumns()
        {
            var values = CsvRowParser.Parse("ui.story.line,\"First, pause\",\"He said \"\"again\"\"\"");

            Assert.That(values, Is.EqualTo(new[] { "ui.story.line", "First, pause", "He said \"again\"" }));
        }
    }
}
