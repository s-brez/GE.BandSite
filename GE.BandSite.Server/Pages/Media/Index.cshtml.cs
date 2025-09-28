using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Media;

/// <summary>
/// Showcases highlight media assets for the band.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IMediaQueryService _mediaQueryService;

    public IndexModel(IMediaQueryService mediaQueryService)
    {
        _mediaQueryService = mediaQueryService;
    }

    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroLead { get; private set; } = string.Empty;

    public MediaItem? FeaturedVideo { get; private set; }

    public IReadOnlyList<MediaItem> PhotoGallery { get; private set; } = Array.Empty<MediaItem>();

    public IReadOnlyList<MediaItem> VideoGallery { get; private set; } = Array.Empty<MediaItem>();

    public async Task OnGetAsync()
    {
        HeroTitle = "Watch the dance floor ignite.";
        HeroLead = "Check out Swing the Boogie in action.";

        var gallery = await _mediaQueryService.GetGalleryAsync().ConfigureAwait(false);

        FeaturedVideo = gallery.Videos.FirstOrDefault();
        PhotoGallery = gallery.Photos;
        VideoGallery = gallery.Videos;
    }
}
