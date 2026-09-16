using System;
using System.Collections.Generic;
using System.Linq;
using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class RenameEngineTests
    {
        private static IReadOnlyList<RenameResult> Plan(string[] names, RenameOptions o) =>
            RenameEngine.Plan(names, o);

        [Fact]
        public void Literal_ReplacesInNameOnly_PreservesExtension()
        {
            var r = Plan(new[] { "IMG_1.jpg" },
                new RenameOptions { Search = "IMG", Replace = "Photo" });

            Assert.Equal("Photo_1.jpg", r[0].Proposed);
            Assert.True(r[0].Changed);
        }

        [Fact]
        public void Literal_CaseInsensitiveByDefault_CaseSensitiveWhenSet()
        {
            var ci = Plan(new[] { "img.png" }, new RenameOptions { Search = "IMG", Replace = "X" });
            Assert.Equal("X.png", ci[0].Proposed);

            var cs = Plan(new[] { "img.png" },
                new RenameOptions { Search = "IMG", Replace = "X", CaseSensitive = true });
            Assert.Equal("img.png", cs[0].Proposed);
            Assert.False(cs[0].Changed);
        }

        [Fact]
        public void Literal_MatchFirstOnly_WhenNotMatchAll()
        {
            var all = Plan(new[] { "a-a-a.jpg" }, new RenameOptions { Search = "a", Replace = "b" });
            Assert.Equal("b-b-b.jpg", all[0].Proposed);

            var first = Plan(new[] { "a-a-a.jpg" },
                new RenameOptions { Search = "a", Replace = "b", MatchAllOccurrences = false });
            Assert.Equal("b-a-a.jpg", first[0].Proposed);
        }

        [Fact]
        public void Regex_WithGroupSubstitution()
        {
            var r = Plan(new[] { "2026-09-16.jpg" },
                new RenameOptions
                {
                    Search = @"(\d{4})-(\d{2})-(\d{2})",
                    Replace = "$3.$2.$1",
                    UseRegex = true,
                });

            Assert.Equal("16.09.2026.jpg", r[0].Proposed);
        }

        [Fact]
        public void Regex_Invalid_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                Plan(new[] { "a.jpg" }, new RenameOptions { Search = "(", UseRegex = true }));
        }

        [Fact]
        public void Target_ExtensionOnly()
        {
            var r = Plan(new[] { "photo.jpeg" },
                new RenameOptions { Search = "jpeg", Replace = "jpg", Target = RenameTarget.ExtensionOnly });

            Assert.Equal("photo.jpg", r[0].Proposed);
        }

        [Fact]
        public void Target_NameAndExtension()
        {
            var r = Plan(new[] { "a.a.txt" },
                new RenameOptions { Search = "a", Replace = "b", Target = RenameTarget.NameAndExtension });

            Assert.Equal("b.b.txt", r[0].Proposed);
        }

        [Fact]
        public void Counter_ExpandsAndAdvancesOnlyForChangedFiles()
        {
            var r = Plan(
                new[] { "IMG_1.jpg", "skip.png", "IMG_2.jpg" },
                new RenameOptions { Search = "IMG_\\d+", Replace = "Pic${n}", UseRegex = true, CounterStart = 1 });

            Assert.Equal("Pic1.jpg", r[0].Proposed);
            Assert.Equal("skip.png", r[1].Proposed);   // no match → unchanged, counter not consumed
            Assert.False(r[1].Changed);
            Assert.Equal("Pic2.jpg", r[2].Proposed);
        }

        [Fact]
        public void Counter_Padding()
        {
            var r = Plan(
                new[] { "p1.jpg", "p2.jpg" },
                new RenameOptions { Search = @"p\d", Replace = "img${n}", UseRegex = true, CounterStart = 9, CounterPadding = 3 });

            Assert.Equal("img009.jpg", r[0].Proposed);
            Assert.Equal("img010.jpg", r[1].Proposed);
        }

        [Fact]
        public void EmptySearch_LeavesEverythingUnchanged()
        {
            var r = Plan(new[] { "a.jpg", "b.png" }, new RenameOptions { Search = "", Replace = "x" });
            Assert.All(r, x => Assert.False(x.Changed));
        }
    }
}
