using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GE.BandSite.Server.Configuration;

namespace GE.BandSite.Server.Features.Media.Processing;

public sealed class FfmpegMediaTranscoder : IMediaTranscoder
{
    private readonly MediaProcessingOptions _options;
    private readonly ILogger<FfmpegMediaTranscoder> _logger;

    public FfmpegMediaTranscoder(IOptions<MediaProcessingOptions> options, ILogger<FfmpegMediaTranscoder> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        var input = request.InputPath;
        var output = request.OutputPath;

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        if (!File.Exists(input))
        {
            throw new FileNotFoundException("Video source could not be found.", input);
        }

        var ffmpegPath = _options.FfmpegPath;
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            _logger.LogWarning("FFmpeg path not configured. Falling back to direct copy for {Input}.", input);
            File.Copy(input, output, overwrite: true);
            return MediaTranscodeResultDefaults.Empty;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -i \"{input}\" -c:v libx264 -preset veryfast -crf 20 -movflags +faststart \"{output}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start ffmpeg process.");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {error}");
            }
        }
        catch (Exception exception) when (exception is Win32Exception || exception is InvalidOperationException)
        {
            _logger.LogWarning(exception, "FFmpeg execution failed; falling back to direct copy for {Input}.", input);
            File.Copy(input, output, overwrite: true);
            return MediaTranscodeResultDefaults.Empty;
        }

        return MediaTranscodeResultDefaults.Empty;
    }
}
