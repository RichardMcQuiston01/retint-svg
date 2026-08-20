using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class OrientationTests
    {
        [Theory]
        [InlineData(1, OrientationTransform.None)]
        [InlineData(2, OrientationTransform.FlipHorizontal)]
        [InlineData(3, OrientationTransform.Rotate180)]
        [InlineData(4, OrientationTransform.FlipVertical)]
        [InlineData(5, OrientationTransform.Transpose)]
        [InlineData(6, OrientationTransform.Rotate90)]
        [InlineData(7, OrientationTransform.Transverse)]
        [InlineData(8, OrientationTransform.Rotate270)]
        public void ToTransform_MapsKnownExifValues(int exif, OrientationTransform expected) =>
            Assert.Equal(expected, ExifOrientation.ToTransform(exif));

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        [InlineData(-1)]
        [InlineData(255)]
        public void ToTransform_UnknownValue_IsNone(int exif) =>
            Assert.Equal(OrientationTransform.None, ExifOrientation.ToTransform(exif));

        [Theory]
        [InlineData(OrientationTransform.Rotate90, true)]
        [InlineData(OrientationTransform.Rotate270, true)]
        [InlineData(OrientationTransform.Transpose, true)]
        [InlineData(OrientationTransform.Transverse, true)]
        [InlineData(OrientationTransform.None, false)]
        [InlineData(OrientationTransform.Rotate180, false)]
        [InlineData(OrientationTransform.FlipHorizontal, false)]
        [InlineData(OrientationTransform.FlipVertical, false)]
        public void SwapsWidthHeight_TrueOnlyForQuarterTurns(OrientationTransform t, bool expected) =>
            Assert.Equal(expected, ExifOrientation.SwapsWidthHeight(t));
    }
}
