using System;
using System.Collections.Generic;
using System.IO;
using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class OutputNamingTests
    {
        // Paths are built with Path.Combine so the tests are correct on any OS
        // (CI runs them on Linux), and assertions compare only the file name.

        [Fact]
        public void NoCollision_UsesTokenSuffix_PreservesExtension()
        {
            var src = Path.Combine("pics", "Photo.jpg");

            var result = OutputNaming.BuildOutputPath(src, "50pct", _ => false);

            Assert.Equal("Photo_50pct.jpg", Path.GetFileName(result));
            Assert.Equal(Path.Combine("pics"), Path.GetDirectoryName(result));
        }

        [Fact]
        public void FirstCollision_AppendsCounter2()
        {
            var src = Path.Combine("pics", "Photo.jpg");
            var taken = new HashSet<string> { "Photo_50pct.jpg" };

            var result = OutputNaming.BuildOutputPath(
                src, "50pct", p => taken.Contains(Path.GetFileName(p)!));

            Assert.Equal("Photo_50pct_2.jpg", Path.GetFileName(result));
        }

        [Fact]
        public void MultipleCollisions_IncrementsUntilFree()
        {
            var src = Path.Combine("pics", "Photo.jpg");
            var taken = new HashSet<string>
            {
                "Photo_50pct.jpg",
                "Photo_50pct_2.jpg",
                "Photo_50pct_3.jpg",
            };

            var result = OutputNaming.BuildOutputPath(
                src, "50pct", p => taken.Contains(Path.GetFileName(p)!));

            Assert.Equal("Photo_50pct_4.jpg", Path.GetFileName(result));
        }

        [Fact]
        public void PreservesOriginalExtensionCasing()
        {
            var src = Path.Combine("pics", "Photo.PNG");

            var result = OutputNaming.BuildOutputPath(src, "1024px", _ => false);

            Assert.Equal("Photo_1024px.PNG", Path.GetFileName(result));
        }

        [Fact]
        public void HandlesFileWithNoDirectory()
        {
            var result = OutputNaming.BuildOutputPath("Photo.gif", "25pct", _ => false);

            Assert.Equal("Photo_25pct.gif", Path.GetFileName(result));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("50/pct")]   // forward slash
        [InlineData("50\\pct")]  // backslash
        public void InvalidToken_Throws(string? token)
        {
            var src = Path.Combine("pics", "Photo.jpg");

            Assert.Throws<ArgumentException>(
                () => OutputNaming.BuildOutputPath(src, token!, _ => false));
        }
    }
}
