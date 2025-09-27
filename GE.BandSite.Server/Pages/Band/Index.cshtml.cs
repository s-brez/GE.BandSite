using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Server.Features.Organization.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Band;

/// <summary>
/// Shares lineup information for the ten-piece Swing The Boogie ensemble.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IOrganizationContentService _organizationContent;

    public IndexModel(IOrganizationContentService organizationContent)
    {
        _organizationContent = organizationContent;
    }

    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroLead { get; private set; } = string.Empty;

    public IReadOnlyList<BandMemberModel> BandMembers { get; private set; } = Array.Empty<BandMemberModel>();

    public IReadOnlyList<BandConfiguration> Configurations { get; private set; } = Array.Empty<BandConfiguration>();

    public IReadOnlyList<string> TouringHighlights { get; private set; } = Array.Empty<string>();

    public async Task OnGetAsync()
    {
        HeroTitle = "Meet the ten artists behind the Swing The Boogie engine.";
        HeroLead = "Gilbert Ernest curates a roster of virtuosos who can scale from speakeasy duos to full horn showcases without missing the groove.";

        BandMembers = await _organizationContent.GetActiveBandMembersAsync().ConfigureAwait(false);

        Configurations = new List<BandConfiguration>
        {
            new("Solo & Duo", "Piano, vocals, or violin pairings perfect for ceremonies, cocktail hours, and intimate dinners."),
            new("Swing Quintet", "Five-piece rhythm + horns delivering compact energy for boutique venues."),
            new("10-Piece Showcase", "Full horn section, dual vocals, and rhythm powerhouse built for grand ballrooms and festivals."),
        };

        TouringHighlights = new List<string>
        {
            "Residencies at the Monaco Jazz Pavilion and Singapore Sky Lounge.",
            "Corporate premieres for Aston Martin, LVMH, and international fintech summits.",
            "Destination weddings from Lake Como villas to coastal Byron Bay retreats.",
        };
    }

    public sealed record BandConfiguration(string Name, string Description);
}
