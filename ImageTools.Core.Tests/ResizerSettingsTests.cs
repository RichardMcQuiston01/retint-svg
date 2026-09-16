using System.IO;
using System.Linq;
using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class ResizerSettingsTests
    {
        [Fact]
        public void EnsureFileExists_CreatesTemplateWhenMissing_AndPreservesExisting()
        {
            var dir = Path.Combine(Path.GetTempPath(), "svgtools-test-" + Path.GetRandomFileName());
            var path = Path.Combine(dir, "resizer-settings.ini");
            try
            {
                // Missing → seeded from the default template (parses to defaults).
                var returned = ResizerSettings.EnsureFileExists(path);
                Assert.Equal(path, returned);
                Assert.True(File.Exists(path));
                var seeded = ResizerSettings.Parse(File.ReadAllText(path));
                Assert.Equal(SizePreset.Defaults.Count, seeded.Presets.Count);

                // Existing → left untouched.
                File.WriteAllText(path, "[presets]\nOnly = 10%\n");
                ResizerSettings.EnsureFileExists(path);
                var kept = ResizerSettings.Parse(File.ReadAllText(path));
                Assert.Single(kept.Presets);
                Assert.Equal("Only", kept.Presets[0].Label);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
            }
        }

        [Fact]
        public void Parse_ReadsSettingsAndPresets()
        {
            var text = @"
; a comment
[settings]
jpeg-quality = 70
allow-upscale = false

[presets]
Half = 50%
Web = 800x600
Big edge = 1600px
";
            var s = ResizerSettings.Parse(text);

            Assert.Equal(70, s.JpegQuality);
            Assert.False(s.AllowUpscale);

            Assert.Equal(3, s.Presets.Count);
            Assert.Equal("Half", s.Presets[0].Label);
            Assert.Equal(SizeKind.Percent, s.Presets[0].Spec.Kind);
            Assert.Equal("Web", s.Presets[1].Label);
            Assert.Equal(SizeKind.ExactWidthHeight, s.Presets[1].Spec.Kind);
            Assert.Equal("Big edge", s.Presets[2].Label);
            Assert.Equal(SizeKind.LongestEdge, s.Presets[2].Spec.Kind);
        }

        [Fact]
        public void Parse_SkipsBadPresetLinesButKeepsGoodOnes()
        {
            var text = @"
[presets]
Good = 50%
Bad = nonsense
AlsoGood = 1024px
NoEquals
";
            var s = ResizerSettings.Parse(text);

            Assert.Equal(2, s.Presets.Count);
            Assert.Contains(s.Presets, p => p.Label == "Good");
            Assert.Contains(s.Presets, p => p.Label == "AlsoGood");
        }

        [Fact]
        public void Parse_NoValidPresets_FallsBackToDefaults()
        {
            var s = ResizerSettings.Parse("[presets]\nBad = nope\n");
            Assert.Equal(SizePreset.Defaults.Count, s.Presets.Count);
        }

        [Fact]
        public void Parse_ClampsJpegQualityIntoRange()
        {
            Assert.Equal(100, ResizerSettings.Parse("[settings]\njpeg-quality = 500\n").JpegQuality);
            Assert.Equal(1, ResizerSettings.Parse("[settings]\njpeg-quality = -5\n").JpegQuality);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("YES", true)]
        [InlineData("on", true)]
        [InlineData("false", false)]
        [InlineData("0", false)]
        [InlineData("off", false)]
        public void Parse_AllowUpscaleBooleanForms(string value, bool expected)
        {
            var s = ResizerSettings.Parse($"[settings]\nallow-upscale = {value}\n");
            Assert.Equal(expected, s.AllowUpscale);
        }

        [Fact]
        public void Parse_KeysOutsideKnownSectionsIgnored()
        {
            // "50%" here is a value in an unknown section, not a preset.
            var s = ResizerSettings.Parse("[other]\nHalf = 50%\n");
            Assert.Equal(SizePreset.Defaults.Count, s.Presets.Count); // fell back
        }

        [Fact]
        public void DefaultFileTemplate_RoundTripsToDefaultPresets()
        {
            var s = ResizerSettings.Parse(ResizerSettings.DefaultFileTemplate());

            Assert.Equal(85, s.JpegQuality);
            Assert.True(s.AllowUpscale);
            Assert.Equal(SizePreset.Defaults.Count, s.Presets.Count);
            Assert.Equal(
                SizePreset.Defaults.Select(p => p.Label),
                s.Presets.Select(p => p.Label));
        }

        [Fact]
        public void Defaults_AreSensible()
        {
            Assert.Equal(85, ResizerSettings.Defaults.JpegQuality);
            Assert.True(ResizerSettings.Defaults.AllowUpscale);
            Assert.NotEmpty(ResizerSettings.Defaults.Presets);
        }
    }
}
