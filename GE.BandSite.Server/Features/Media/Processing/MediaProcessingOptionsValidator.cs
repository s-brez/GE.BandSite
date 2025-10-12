using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Features.Media.Processing;

public sealed class MediaProcessingOptionsValidator : IValidateOptions<MediaProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.GetFfmpegPath()))
        {
            return ValidateOptionsResult.Fail("MediaProcessing FFMPEG path must be configured for the current platform (set FfmpegPath*, or the cross-platform FfmpegPath).");
        }

        if (options.PhotoOptimizationEnabled)
        {
            if (options.PhotoMaxWidth < 0 || options.PhotoMaxHeight < 0)
            {
                return ValidateOptionsResult.Fail("MediaProcessing photo dimensions must be zero or positive values.");
            }

            if (options.PhotoJpegQuality is < 30 or > 100)
            {
                return ValidateOptionsResult.Fail("MediaProcessing photo quality must be between 30 and 100.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
