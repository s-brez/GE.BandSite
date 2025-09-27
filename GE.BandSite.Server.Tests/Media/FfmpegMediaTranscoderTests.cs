using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Tests.Media;

[TestFixture]
public class FfmpegMediaTranscoderTests
{
    [Test]
    public async Task TranscodeAsync_WhenFfmpegPathMissing_FallsBackToCopy()
    {
        var input = Path.Combine(Path.GetTempPath(), $"ffmpeg-input-{Guid.NewGuid():N}.mp4");
        var output = Path.Combine(Path.GetTempPath(), $"ffmpeg-output-{Guid.NewGuid():N}.mp4");
        await File.WriteAllTextAsync(input, "sample content");

        try
        {
            var options = Options.Create(new MediaProcessingOptions { FfmpegPath = null });
            var transcoder = new FfmpegMediaTranscoder(options, NullLogger<FfmpegMediaTranscoder>.Instance);

            var result = await transcoder.TranscodeAsync(new MediaTranscodeRequest(input, output));

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(output), Is.True);
                Assert.That(result.DurationSeconds, Is.Null);
            });
        }
        finally
        {
            if (File.Exists(input))
            {
                File.Delete(input);
            }

            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }
}
