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
        HeroTitle = "Catch Us Live";
        HeroLead = "Want to see Swing The Boogie in action? Here’s where you can experience the band before you book.";

        UpcomingEvents = await _organizationContent.GetPublishedEventsAsync().ConfigureAwait(false);

        BookingNotes = new List<string>
        {
            "Private showcases can be curated on request for planners and corporate buyers.",
            "Join the newsletter for presale windows and behind-the-scenes media drops.",
        };
    }
}
