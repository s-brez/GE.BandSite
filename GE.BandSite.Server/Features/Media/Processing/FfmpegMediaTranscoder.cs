using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Services.Processes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Features.Media.Processing;

public sealed class FfmpegMediaTranscoder : IMediaTranscoder
{
    private readonly MediaProcessingOptions _options;
    private readonly ILogger<FfmpegMediaTranscoder> _logger;
    private readonly IExternalProcessRunner _processRunner;

    public FfmpegMediaTranscoder(
        IOptions<MediaProcessingOptions> options,
        ILogger<FfmpegMediaTranscoder> logger,
        IExternalProcessRunner processRunner)
    {
        _options = options.Value;
        _logger = logger;
        _processRunner = processRunner;
    }

    public async Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        if (!File.Exists(request.InputPath))
        {
            throw new FileNotFoundException("Video source could not be found.", request.InputPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);

        var inputExtension = Path.GetExtension(request.InputPath);
        var requiresTranscode = !string.Equals(inputExtension, ".mp4", StringComparison.OrdinalIgnoreCase);

        if (requiresTranscode)
        {
            await RunFfmpegAsync(request.InputPath, request.OutputPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Input {Input} already MP4; performing direct copy.", request.InputPath);
            File.Copy(request.InputPath, request.OutputPath, overwrite: true);
        }

        if (!File.Exists(request.OutputPath))
        {
            throw new InvalidOperationException($"FFmpeg did not produce the expected output file '{request.OutputPath}'.");
        }

        return await ReadMetadataAsync(request.OutputPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunFfmpegAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var ffmpegPath = ResolveRequiredPath(_options.FfmpegPath, nameof(MediaProcessingOptions.FfmpegPath));
        var arguments = $"-y -i \"{inputPath}\" -c:v libx264 -preset veryfast -crf 20 -movflags +faststart -c:a aac -b:a 192k \"{outputPath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Starting FFmpeg transcode for {Input} -> {Output}.", inputPath, outputPath);
        var result = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg exited with code {result.ExitCode}: {result.StandardError}");
        }

        _logger.LogInformation("FFmpeg completed successfully for {Input}.", inputPath);
    }

    private async Task<MediaTranscodeResult> ReadMetadataAsync(string outputPath, CancellationToken cancellationToken)
    {
        var ffprobePath = ResolveFfprobePath();
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            _logger.LogWarning("ffprobe path not configured; skipping metadata extraction for {Output}.", outputPath);
            return MediaTranscodeResultDefaults.Empty;
        }

        var arguments = $"-v quiet -print_format json -show_streams -show_format \"{outputPath}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            var result = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                _logger.LogWarning("ffprobe exited with code {ExitCode} for {Output}. stderr: {Error}", result.ExitCode, outputPath, result.StandardError);
                return MediaTranscodeResultDefaults.Empty;
            }

            return ParseMetadata(result.StandardOutput);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to inspect metadata for {Output}.", outputPath);
            return MediaTranscodeResultDefaults.Empty;
        }
    }

    private MediaTranscodeResult ParseMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MediaTranscodeResultDefaults.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            int? durationSeconds = null;
            if (document.RootElement.TryGetProperty("format", out var formatElement) &&
                formatElement.TryGetProperty("duration", out var durationElement) &&
                durationElement.ValueKind == JsonValueKind.String &&
                double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            {
                durationSeconds = (int)Math.Round(duration, MidpointRounding.AwayFromZero);
            }

            int? width = null;
            int? height = null;
            if (document.RootElement.TryGetProperty("streams", out var streamsElement) && streamsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streamsElement.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var codecTypeElement) &&
                        string.Equals(codecTypeElement.GetString(), "video", StringComparison.OrdinalIgnoreCase))
                    {
                        if (stream.TryGetProperty("width", out var widthElement) && widthElement.TryGetInt32(out var parsedWidth))
                        {
                            width = parsedWidth;
                        }

                        if (stream.TryGetProperty("height", out var heightElement) && heightElement.TryGetInt32(out var parsedHeight))
                        {
                            height = parsedHeight;
                        }

                        break;
                    }
                }
            }

            return new MediaTranscodeResult(durationSeconds, width, height);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Failed to parse ffprobe metadata. Payload length {Length} characters.", json.Length);
            return MediaTranscodeResultDefaults.Empty;
        }
    }

    private string ResolveRequiredPath(string? configuredPath, string optionName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"{optionName} must be configured when media processing is enabled.");
        }

        return configuredPath;
    }

    private string? ResolveFfprobePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.FfprobePath))
        {
            return _options.FfprobePath;
        }

        if (string.IsNullOrWhiteSpace(_options.FfmpegPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(_options.FfmpegPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "ffprobe";
        }

        var extension = Path.GetExtension(_options.FfmpegPath);
        var probeFileName = string.IsNullOrWhiteSpace(extension) ? "ffprobe" : $"ffprobe{extension}";
        return Path.Combine(directory, probeFileName);
    }
}
