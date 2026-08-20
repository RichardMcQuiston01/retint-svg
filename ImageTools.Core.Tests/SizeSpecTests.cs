using System;
using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class SizeSpecTests
    {
        // ── Percent ──────────────────────────────────────────────────────────

        [Fact]
        public void Percent_50_HalvesBothDimensions()
        {
            var result = SizeSpec.FromPercent(50).Compute(1000, 800);
            Assert.Equal(new Dimensions(500, 400), result);
        }

        [Fact]
        public void Percent_200_DoublesBothDimensions()
        {
            var result = SizeSpec.FromPercent(200).Compute(100, 50);
            Assert.Equal(new Dimensions(200, 100), result);
        }

        [Fact]
        public void Percent_RoundsHalfAwayFromZero()
        {
            // 3 * 0.5 = 1.5 -> rounds up to 2
            var result = SizeSpec.FromPercent(50).Compute(3, 3);
            Assert.Equal(new Dimensions(2, 2), result);
        }

        [Fact]
        public void Percent_ClampsToMinimumOnePixel()
        {
            // 10 * 0.01 = 0.1 -> rounds to 0 -> clamped to 1
            var result = SizeSpec.FromPercent(1).Compute(10, 10);
            Assert.Equal(new Dimensions(1, 1), result);
        }

        [Fact]
        public void Percent_NoUpscale_ReturnsSourceUnchanged()
        {
            var result = SizeSpec.FromPercent(200).Compute(100, 120, allowUpscale: false);
            Assert.Equal(new Dimensions(100, 120), result);
        }

        [Fact]
        public void Percent_NoUpscale_StillAllowsDownscale()
        {
            var result = SizeSpec.FromPercent(50).Compute(100, 120, allowUpscale: false);
            Assert.Equal(new Dimensions(50, 60), result);
        }

        // ── LongestEdge ──────────────────────────────────────────────────────

        [Fact]
        public void LongestEdge_Landscape_ScalesByWidth_PreservesAspect()
        {
            var result = SizeSpec.FromLongestEdge(1000).Compute(2000, 1000);
            Assert.Equal(new Dimensions(1000, 500), result);
        }

        [Fact]
        public void LongestEdge_Portrait_ScalesByHeight_PreservesAspect()
        {
            var result = SizeSpec.FromLongestEdge(1000).Compute(1000, 2000);
            Assert.Equal(new Dimensions(500, 1000), result);
        }

        [Fact]
        public void LongestEdge_Upscale_AllowedByDefault()
        {
            var result = SizeSpec.FromLongestEdge(2000).Compute(1000, 500);
            Assert.Equal(new Dimensions(2000, 1000), result);
        }

        [Fact]
        public void LongestEdge_NoUpscale_SmallerSourceUnchanged()
        {
            var result = SizeSpec.FromLongestEdge(2000).Compute(800, 600, allowUpscale: false);
            Assert.Equal(new Dimensions(800, 600), result);
        }

        // ── ExactWidthHeight ─────────────────────────────────────────────────

        [Fact]
        public void Exact_ReturnsGivenDimensions_IgnoringSource()
        {
            var result = SizeSpec.FromExact(640, 480).Compute(1000, 1000);
            Assert.Equal(new Dimensions(640, 480), result);
        }

        // ── Guards ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0, 100)]
        [InlineData(100, 0)]
        [InlineData(-5, 100)]
        public void Compute_NonPositiveSource_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SizeSpec.FromPercent(50).Compute(width, height));
        }

        [Fact]
        public void Compute_OverflowingDimension_Throws()
        {
            // 1e12 % of a 1,000,000 px edge => 1e16, far beyond Int32.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SizeSpec.FromPercent(1e12).Compute(1_000_000, 1_000_000));
        }

        [Fact]
        public void Compute_NonFinitePercent_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SizeSpec.FromPercent(double.PositiveInfinity).Compute(100, 100));
        }

        // ── Tokens ───────────────────────────────────────────────────────────

        [Fact]
        public void ToToken_Percent_Whole() =>
            Assert.Equal("50pct", SizeSpec.FromPercent(50).ToToken());

        [Fact]
        public void ToToken_Percent_Fractional() =>
            Assert.Equal("12.5pct", SizeSpec.FromPercent(12.5).ToToken());

        [Fact]
        public void ToToken_LongestEdge() =>
            Assert.Equal("1024px", SizeSpec.FromLongestEdge(1024).ToToken());

        [Fact]
        public void ToToken_Exact() =>
            Assert.Equal("640x480", SizeSpec.FromExact(640, 480).ToToken());
    }
}
