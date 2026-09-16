using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class ExifToolCommandTests
    {
        // ── QuoteArgument (Windows CommandLineToArgvW rules) ────────────────────

        [Fact]
        public void Quote_SimpleToken_NotQuoted()
        {
            Assert.Equal("abc", ExifToolCommand.QuoteArgument("abc"));
            Assert.Equal("-EXIF:Artist", ExifToolCommand.QuoteArgument("-EXIF:Artist"));
        }

        [Fact]
        public void Quote_Empty_BecomesEmptyQuotes()
        {
            Assert.Equal("\"\"", ExifToolCommand.QuoteArgument(""));
            Assert.Equal("\"\"", ExifToolCommand.QuoteArgument(null!));
        }

        [Fact]
        public void Quote_WithSpace_IsQuoted()
        {
            Assert.Equal("\"a b\"", ExifToolCommand.QuoteArgument("a b"));
        }

        [Fact]
        public void Quote_EmbeddedQuote_IsEscaped()
        {
            // a"b  ->  "a\"b"
            Assert.Equal("\"a\\\"b\"", ExifToolCommand.QuoteArgument("a\"b"));
        }

        [Fact]
        public void Quote_TrailingBackslashes_AreDoubledBeforeClosingQuote()
        {
            // "a b\"  ->  the trailing backslash is doubled so it doesn't escape the quote
            Assert.Equal("\"a b\\\\\"", ExifToolCommand.QuoteArgument("a b\\"));
        }

        // ── BuildReadArguments ──────────────────────────────────────────────────

        [Fact]
        public void Read_IncludesCsvSepAndEveryTag()
        {
            var args = ExifToolCommand.BuildReadArguments(@"C:\pics\a.jpg");

            Assert.Contains("-csv", args);
            Assert.Contains("-sep \", \"", args);   // separator quoted (it has a space)
            foreach (var f in ExifToolCommand.Fields)
                Assert.Contains("-" + f.Tag, args);
            Assert.EndsWith(@"C:\pics\a.jpg", args); // no space => path unquoted, comes last
        }

        [Fact]
        public void Read_PathWithSpace_IsQuoted()
        {
            var args = ExifToolCommand.BuildReadArguments(@"C:\my pics\a.jpg");
            Assert.EndsWith("\"C:\\my pics\\a.jpg\"", args);
        }

        // ── BuildWriteArguments ─────────────────────────────────────────────────

        [Fact]
        public void Write_AssignsEachTag_QuotingValuesWithSpaces()
        {
            var m = new ImageMetadata
            {
                Artist = "Jane Doe",
                Copyright = "",                 // empty => clears the tag (no quoting)
                Keywords = "k1, k2",
            };
            var args = ExifToolCommand.BuildWriteArguments(@"C:\pics\a.jpg", m);

            Assert.Contains("\"-EXIF:Artist=Jane Doe\"", args);
            Assert.Contains("-EXIF:Copyright=", args);          // cleared, no space, unquoted
            Assert.Contains("\"-IPTC:Keywords=k1, k2\"", args); // separator space => quoted
            Assert.Contains("-P", args);                         // preserve file dates
            Assert.Contains("-codedcharacterset=utf8", args);
            Assert.DoesNotContain("-overwrite_original", args); // keeps the *_original backup
        }

        // ── ReadFromCsv (map ExifTool -csv output back to the DTO) ───────────────

        [Fact]
        public void ReadFromCsv_MapsColumnsByOrder_HandlesQuotedValues()
        {
            var csv =
                "SourceFile,Artist,Copyright,ImageDescription,DateTimeOriginal,ObjectName,Caption-Abstract,Keywords,By-line,City,Country-PrimaryLocationName\n" +
                "a.jpg,Jane,\"(c) 2026\",\"A, B\",2026:01:02 03:04:05,Title,\"Long caption\",\"k1, k2\",Bob,NYC,USA\n";

            var m = ExifToolCommand.ReadFromCsv(csv);

            Assert.Equal("Jane", m.Artist);
            Assert.Equal("(c) 2026", m.Copyright);
            Assert.Equal("A, B", m.Description);          // embedded comma, from a quoted field
            Assert.Equal("2026:01:02 03:04:05", m.DateTaken);
            Assert.Equal("Title", m.Title);
            Assert.Equal("Long caption", m.Caption);
            Assert.Equal("k1, k2", m.Keywords);
            Assert.Equal("Bob", m.Creator);
            Assert.Equal("NYC", m.City);
            Assert.Equal("USA", m.Country);
        }

        [Fact]
        public void ReadFromCsv_NoDataRow_ReturnsEmpty()
        {
            var m = ExifToolCommand.ReadFromCsv("SourceFile,Artist\n");
            Assert.Equal("", m.Artist);

            var empty = ExifToolCommand.ReadFromCsv("");
            Assert.Equal("", empty.Artist);
        }

        [Fact]
        public void ReadFromCsv_ShortRow_LeavesMissingFieldsEmpty()
        {
            // Only SourceFile + Artist present; the rest stay empty rather than throwing.
            var m = ExifToolCommand.ReadFromCsv("SourceFile,Artist\na.jpg,Jane\n");
            Assert.Equal("Jane", m.Artist);
            Assert.Equal("", m.Country);
        }
    }
}
