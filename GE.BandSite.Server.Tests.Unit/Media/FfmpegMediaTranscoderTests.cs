using System.Diagnostics;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Processing;
using GE.BandSite.Server.Services.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Tests.Media;

[TestFixture]
public class FfmpegMediaTranscoderTests
{
    [Test]
    public async Task TranscodeAsync_WithMovInput_InvokesFfmpegAndParsesMetadata()
    {
        var input = Path.Combine(Path.GetTempPath(), $"ffmpeg-input-{Guid.NewGuid():N}.mov");
        var output = Path.Combine(Path.GetTempPath(), $"ffmpeg-output-{Guid.NewGuid():N}.mp4");
        await File.WriteAllTextAsync(input, "mov-content");

        var ffmpegCalled = false;
        var ffprobeCalled = false;

        var runner = new StubProcessRunner(info =>
        {
            if (info.FileName == "ffmpeg")
            {
                ffmpegCalled = true;
                Assert.That(info.Arguments, Does.Contain(input));
                Assert.That(info.Arguments, Does.Contain(output));
                File.WriteAllText(output, "mp4-data");
                return new ExternalProcessResult(0, string.Empty, string.Empty);
            }

            if (info.FileName == "ffprobe")
            {
                ffprobeCalled = true;
                var metadata = "{\"format\":{\"duration\":\"12.5\"},\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080}]}";
                return new ExternalProcessResult(0, metadata, string.Empty);
            }

            throw new InvalidOperationException($"Unexpected process invocation: {info.FileName}");
        });

        var options = Options.Create(new MediaProcessingOptions
        {
            FfmpegPath = "ffmpeg",
            FfprobePath = "ffprobe"
        });

        var transcoder = new FfmpegMediaTranscoder(options, NullLogger<FfmpegMediaTranscoder>.Instance, runner);

        try
        {
            var result = await transcoder.TranscodeAsync(new MediaTranscodeRequest(input, output));

            Assert.Multiple(() =>
            {
                Assert.That(ffmpegCalled, Is.True, "ffmpeg should be invoked for MOV sources.");
                Assert.That(ffprobeCalled, Is.True, "ffprobe should be invoked for metadata.");
                Assert.That(File.Exists(output), Is.True);
                Assert.That(result.DurationSeconds, Is.EqualTo(13));
                Assert.That(result.Width, Is.EqualTo(1920));
                Assert.That(result.Height, Is.EqualTo(1080));
            });
        }
        finally
        {
            DeleteIfExists(input);
            DeleteIfExists(output);
        }
    }

    [Test]
    public async Task TranscodeAsync_WithMp4Input_SkipsFfmpegAndCopiesFile()
    {
        var input = Path.Combine(Path.GetTempPath(), $"ffmpeg-input-{Guid.NewGuid():N}.mp4");
        var output = Path.Combine(Path.GetTempPath(), $"ffmpeg-output-{Guid.NewGuid():N}.mp4");
        const string contents = "mp4-content";
        await File.WriteAllTextAsync(input, contents);

        var ffmpegCalled = false;

        var runner = new StubProcessRunner(info =>
        {
            if (info.FileName == "ffmpeg")
            {
                ffmpegCalled = true;
                return new ExternalProcessResult(0, string.Empty, string.Empty);
            }

            if (info.FileName == "ffprobe")
            {
                var metadata = "{\"format\":{\"duration\":\"7\"},\"streams\":[{\"codec_type\":\"video\",\"width\":1280,\"height\":720}]}";
                return new ExternalProcessResult(0, metadata, string.Empty);
            }

            throw new InvalidOperationException($"Unexpected process invocation: {info.FileName}");
        });

        var options = Options.Create(new MediaProcessingOptions
        {
            FfmpegPath = "ffmpeg",
            FfprobePath = "ffprobe"
        });

        var transcoder = new FfmpegMediaTranscoder(options, NullLogger<FfmpegMediaTranscoder>.Instance, runner);

        try
        {
            var result = await transcoder.TranscodeAsync(new MediaTranscodeRequest(input, output));

            Assert.Multiple(() =>
            {
                Assert.That(ffmpegCalled, Is.False, "MP4 sources should bypass ffmpeg transcode.");
                Assert.That(File.ReadAllText(output), Is.EqualTo(contents));
                Assert.That(result.DurationSeconds, Is.EqualTo(7));
                Assert.That(result.Width, Is.EqualTo(1280));
                Assert.That(result.Height, Is.EqualTo(720));
            });
        }
        finally
        {
            DeleteIfExists(input);
            DeleteIfExists(output);
        }
    }

    [Test]
    public void TranscodeAsync_WithoutFfmpegPath_Throws()
    {
        var input = Path.Combine(Path.GetTempPath(), $"ffmpeg-input-{Guid.NewGuid():N}.mov");
        var output = Path.Combine(Path.GetTempPath(), $"ffmpeg-output-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(input, "mov-content");

        var options = Options.Create(new MediaProcessingOptions { FfmpegPath = null });
        var runner = new StubProcessRunner(_ => new ExternalProcessResult(0, string.Empty, string.Empty));
        var transcoder = new FfmpegMediaTranscoder(options, NullLogger<FfmpegMediaTranscoder>.Instance, runner);

        try
        {
            Assert.That(() => transcoder.TranscodeAsync(new MediaTranscodeRequest(input, output)), Throws.InvalidOperationException);
        }
        finally
        {
            DeleteIfExists(input);
            DeleteIfExists(output);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class StubProcessRunner : IExternalProcessRunner
    {
        private readonly Func<ProcessStartInfo, ExternalProcessResult> _handler;

        public StubProcessRunner(Func<ProcessStartInfo, ExternalProcessResult> handler)
        {
            _handler = handler;
        }

        public Task<ExternalProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_handler(startInfo));
        }
    }
}
