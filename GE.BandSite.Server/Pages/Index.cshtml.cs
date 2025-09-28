using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace GE.BandSite.Server.Pages;

/// <summary>
/// Provides content for the Swing The Boogie home page.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IMediaQueryService _mediaQueryService;

    public IndexModel(ILogger<IndexModel> logger, IMediaQueryService mediaQueryService)
    {
        _logger = logger;
        _mediaQueryService = mediaQueryService;
    }

    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroLeadCopy { get; private set; } = string.Empty;

    public string CallToActionText { get; private set; } = string.Empty;

    public IReadOnlyList<ValueHighlight> ValueHighlights { get; private set; } = Array.Empty<ValueHighlight>();

    public string HighlightVideoTitle { get; private set; } = string.Empty;

    public string HighlightVideoSummary { get; private set; } = string.Empty;

    public MediaItem? HighlightVideo { get; private set; }

    public string HighlightVideoMimeType { get; private set; } = string.Empty;

    public IReadOnlyList<MediaItem> HighlightPhotos { get; private set; } = Array.Empty<MediaItem>();

    public async Task OnGetAsync()
    {
        HeroTitle = "The world-class swing band bringing the vibe to your event.";
        HeroLeadCopy = "Gilbert Ernest fronts a flexible 1–10 piece ensemble that transforms corporate galas, weddings, and premium celebrations into unforgettable dance floors.";
        CallToActionText = "Book Your Event";

        ValueHighlights = new List<ValueHighlight>
        {
            new("Corporate Events", "Black-tie galas, award nights, and brand launches that demand impeccable swing and polished professionalism."),
            new("Weddings", "First dances through last encores curated to match your story and keep guests on their feet all night."),
            new("Flexible Lineups", "From intimate cocktail duos to roaring 10-piece horn sections, we shape the band around your vision."),
            new("International Experience", "Worldwide touring pedigree with the logistics discipline to deliver seamless experiences on any continent."),
        };

        var homeMedia = await _mediaQueryService.GetHomeHighlightsAsync().ConfigureAwait(false);

        HighlightVideo = homeMedia.FeaturedVideo ?? CreateFallbackHighlightVideo();
        HighlightPhotos = homeMedia.HighlightPhotos;

        HighlightVideoTitle = HighlightVideo?.Title ?? "Watch the highlight reel";
        HighlightVideoSummary = HighlightVideo?.Description ?? "Preview the sound, swagger, and crowd energy from recent stages as we warm up the dedicated media gallery.";
        HighlightVideoMimeType = DetermineMimeType(HighlightVideo?.Url);

        _logger.LogDebug("Home page content prepared with {ValueCount} value highlights.", ValueHighlights.Count);
    }

    public sealed record ValueHighlight(string Title, string Description);

    private static MediaItem CreateFallbackHighlightVideo()
    {
        const string fallbackUrl = "https://swingtheboogie-media.s3.ap-southeast-2.amazonaws.com/videos/STB_PromoMain_Horizontal.mp4";
        return new MediaItem(
            Guid.Empty,
            "Watch the highlight reel",
            "Preview the energy from the latest Swing The Boogie performances while the media gallery warms up.",
            fallbackUrl,
            "/images/media-video-poster.svg",
            Array.Empty<string>(),
            "Video");
    }

    private static string DetermineMimeType(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "video/mp4";
        }

        var candidate = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            candidate = absolute.AbsolutePath;
        }

        var extension = Path.GetExtension(candidate).ToLowerInvariant();

        return extension switch
        {
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            ".ogg" or ".ogv" => "video/ogg",
            _ => "video/mp4"
        };
    }
}
