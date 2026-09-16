using System.Text.Json;
using ImageTools.Core;
using Xunit;

namespace ImageTools.Core.Tests
{
    public class ResizeJobTests
    {
        // The handler serializes a ResizeJob to a temp file and the worker reads
        // it back, both using System.Text.Json. These tests lock in that the DTO
        // survives a round-trip with the (de)serializer the product will use.

        [Fact]
        public void RoundTrips_ThroughSystemTextJson()
        {
            var job = new ResizeJob
            {
                Size = SizeSpec.FromLongestEdge(1024),
                JpegQuality = 90,
                AllowUpscale = false,
                OutputLocation = "sibling",
            };
            job.Files.Add("a.jpg");
            job.Files.Add("b.png");

            var json = JsonSerializer.Serialize(job);
            var back = JsonSerializer.Deserialize<ResizeJob>(json);

            Assert.NotNull(back);
            Assert.Equal(SizeKind.LongestEdge, back!.Size.Kind);
            Assert.Equal(1024, back.Size.LongestEdge);
            Assert.Equal(90, back.JpegQuality);
            Assert.False(back.AllowUpscale);
            Assert.Equal("sibling", back.OutputLocation);
            Assert.Equal(new[] { "a.jpg", "b.png" }, back.Files);
        }

        [Fact]
        public void Defaults_AreSaneForAnEmptyJob()
        {
            var job = new ResizeJob();

            Assert.Equal(85, job.JpegQuality);
            Assert.True(job.AllowUpscale);
            Assert.Empty(job.Files);
            Assert.NotNull(job.Size);
            Assert.Equal("resize", job.Operation);
            Assert.Equal(0, job.RotateDegrees);
        }

        [Fact]
        public void OperationDefaultsToResize_WhenOmittedFromJson()
        {
            // Old job files (pre-rotate) have no Operation/RotateDegrees fields.
            var back = JsonSerializer.Deserialize<ResizeJob>(
                "{\"Size\":{\"Kind\":0,\"Percent\":50},\"Files\":[\"a.jpg\"]}");

            Assert.NotNull(back);
            Assert.Equal("resize", back!.Operation);
            Assert.Equal(0, back.RotateDegrees);
        }

        [Fact]
        public void RotateJob_RoundTrips()
        {
            var job = new ResizeJob { Operation = "rotate", RotateDegrees = 270 };
            job.Files.Add("a.jpg");

            var back = JsonSerializer.Deserialize<ResizeJob>(JsonSerializer.Serialize(job));

            Assert.Equal("rotate", back!.Operation);
            Assert.Equal(270, back.RotateDegrees);
        }

        [Fact]
        public void DeserializedSpec_ComputesCorrectly()
        {
            var json = JsonSerializer.Serialize(SizeSpec.FromPercent(50));
            var spec = JsonSerializer.Deserialize<SizeSpec>(json)!;

            Assert.Equal(new Dimensions(400, 300), spec.Compute(800, 600));
        }
    }
}
