using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GE.BandSite.Server.Features.Media.Processing;

public interface IImageOptimizer
{
    Task<ImageOptimizationResult> OptimizeAsync(string inputPath, string outputPath, ImageOptimizationOptions options, CancellationToken cancellationToken = default);
}

public sealed record ImageOptimizationOptions(int MaxWidth, int MaxHeight, int Quality);

public sealed record ImageOptimizationResult(int Width, int Height);

public sealed class ImageSharpImageOptimizer : IImageOptimizer
{
    public async Task<ImageOptimizationResult> OptimizeAsync(string inputPath, string outputPath, ImageOptimizationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await using var input = File.OpenRead(inputPath);
        using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken).ConfigureAwait(false);

        image.Mutate(ctx => ctx.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;

        var resizeRequired = (options.MaxWidth > 0 && image.Width > options.MaxWidth) || (options.MaxHeight > 0 && image.Height > options.MaxHeight);
        if (resizeRequired)
        {
            var size = new Size(
                options.MaxWidth > 0 ? options.MaxWidth : image.Width,
                options.MaxHeight > 0 ? options.MaxHeight : image.Height);

            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = size,
                Sampler = KnownResamplers.Bicubic
            }));
        }

        var quality = Math.Clamp(options.Quality, 30, 100);
        var encoder = new JpegEncoder
        {
            Quality = quality
        };

        await using var output = File.Create(outputPath);
        await image.SaveAsync(output, encoder, cancellationToken).ConfigureAwait(false);

        return new ImageOptimizationResult(image.Width, image.Height);
    }
}
