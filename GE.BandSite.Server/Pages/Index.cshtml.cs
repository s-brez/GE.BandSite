using System;
using System.Collections.Generic;
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

        HighlightVideo = homeMedia.FeaturedVideo;
        HighlightPhotos = homeMedia.HighlightPhotos;

        HighlightVideoTitle = HighlightVideo?.Title ?? "Watch the highlight reel";
        HighlightVideoSummary = HighlightVideo?.Description ?? "Preview the sound, swagger, and crowd energy from recent stages as we warm up the dedicated media gallery.";

        _logger.LogDebug("Home page content prepared with {ValueCount} value highlights.", ValueHighlights.Count);
    }

    public sealed record ValueHighlight(string Title, string Description);
}
