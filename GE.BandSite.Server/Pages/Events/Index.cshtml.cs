using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Server.Features.Organization.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Events;

/// <summary>
/// Lists upcoming public events and showcases.
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

    public IReadOnlyList<EventListingModel> UpcomingEvents { get; private set; } = Array.Empty<EventListingModel>();

    public IReadOnlyList<string> BookingNotes { get; private set; } = Array.Empty<string>();

    public async Task OnGetAsync()
    {
        HeroTitle = "Catch Swing The Boogie on stage.";
        HeroLead = "Public showcases surface throughout the year. RSVP early or talk to us about hosting the next stop.";

        UpcomingEvents = await _organizationContent.GetPublishedEventsAsync().ConfigureAwait(false);

        BookingNotes = new List<string>
        {
            "Private showcases can be curated on request for planners and corporate buyers.",
            "Join the newsletter for presale windows and behind-the-scenes media drops.",
        };
    }
}
