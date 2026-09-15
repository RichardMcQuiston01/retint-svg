using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class SizeSpecParseTests
    {
        [Theory]
        [InlineData("50%", 50)]
        [InlineData("50pct", 50)]
        [InlineData(" 25 % ", 25)]
        [InlineData("12.5%", 12.5)]
        [InlineData("200PCT", 200)]
        public void TryParse_Percent(string token, double expected)
        {
            Assert.True(SizeSpec.TryParse(token, out var spec));
            Assert.NotNull(spec);
            Assert.Equal(SizeKind.Percent, spec!.Kind);
            Assert.Equal(expected, spec.Percent);
        }

        [Theory]
        [InlineData("1024px", 1024)]
        [InlineData(" 1920 PX ", 1920)]
        public void TryParse_LongestEdge(string token, int expected)
        {
            Assert.True(SizeSpec.TryParse(token, out var spec));
            Assert.NotNull(spec);
            Assert.Equal(SizeKind.LongestEdge, spec!.Kind);
            Assert.Equal(expected, spec.LongestEdge);
        }

        [Theory]
        [InlineData("640x480", 640, 480)]
        [InlineData("800X600", 800, 600)]
        [InlineData(" 1024 x 768 ", 1024, 768)]
        public void TryParse_Exact(string token, int w, int h)
        {
            Assert.True(SizeSpec.TryParse(token, out var spec));
            Assert.NotNull(spec);
            Assert.Equal(SizeKind.ExactWidthHeight, spec!.Kind);
            Assert.Equal(w, spec.Width);
            Assert.Equal(h, spec.Height);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("0%")]        // non-positive
        [InlineData("-50%")]      // non-positive
        [InlineData("0px")]       // non-positive
        [InlineData("640x")]      // missing height
        [InlineData("x480")]      // missing width
        [InlineData("640x480x2")] // too many parts
        [InlineData("640x0")]     // non-positive dimension
        public void TryParse_Rejects(string? token)
        {
            Assert.False(SizeSpec.TryParse(token, out var spec));
            Assert.Null(spec);
        }

        [Fact]
        public void TryParse_RoundTripsToken()
        {
            // ToToken() -> TryParse() should recover an equivalent spec.
            foreach (var original in new[]
            {
                SizeSpec.FromPercent(75),
                SizeSpec.FromLongestEdge(1200),
                SizeSpec.FromExact(320, 240),
            })
            {
                Assert.True(SizeSpec.TryParse(original.ToToken(), out var parsed));
                Assert.NotNull(parsed);
                Assert.Equal(original.Kind, parsed!.Kind);
                Assert.Equal(original.Compute(1000, 800), parsed.Compute(1000, 800));
            }
        }
    }
}
