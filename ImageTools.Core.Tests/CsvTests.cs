using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class CsvTests
    {
        [Fact]
        public void Parse_SimpleRow()
        {
            var rows = Csv.Parse("a,b,c");
            Assert.Single(rows);
            Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        }

        [Fact]
        public void Parse_QuotedFieldWithComma()
        {
            var rows = Csv.Parse("a,\"b,c\",d");
            Assert.Equal(new[] { "a", "b,c", "d" }, rows[0]);
        }

        [Fact]
        public void Parse_DoubledQuoteInsideQuotedField()
        {
            var rows = Csv.Parse("\"she said \"\"hi\"\"\",x");
            Assert.Equal(new[] { "she said \"hi\"", "x" }, rows[0]);
        }

        [Fact]
        public void Parse_NewlineInsideQuotedFieldIsPreserved()
        {
            var rows = Csv.Parse("\"line1\nline2\",b");
            Assert.Single(rows);
            Assert.Equal("line1\nline2", rows[0][0]);
            Assert.Equal("b", rows[0][1]);
        }

        [Fact]
        public void Parse_TwoRows_CRLF_NoTrailingBlankRow()
        {
            var rows = Csv.Parse("a,b\r\nc,d\r\n");
            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "a", "b" }, rows[0]);
            Assert.Equal(new[] { "c", "d" }, rows[1]);
        }

        [Fact]
        public void Parse_TrailingNewline_LF_NoBlankRow()
        {
            var rows = Csv.Parse("a,b\n");
            Assert.Single(rows);
        }

        [Fact]
        public void Parse_EmptyInput_NoRows()
        {
            Assert.Empty(Csv.Parse(""));
            Assert.Empty(Csv.Parse(null!));
        }

        [Fact]
        public void Parse_PreservesEmptyTrailingField()
        {
            var rows = Csv.Parse("a,");
            Assert.Equal(new[] { "a", "" }, rows[0]);
        }
    }
}
