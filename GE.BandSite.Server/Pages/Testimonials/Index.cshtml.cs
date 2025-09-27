using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Server.Features.Organization.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Testimonials;

/// <summary>
/// Surfaces social proof from recent clients.
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

    public IReadOnlyList<TestimonialModel> Testimonials { get; private set; } = Array.Empty<TestimonialModel>();

    public async Task OnGetAsync()
    {
        HeroTitle = "What our clients say.";
        HeroLead = "From luxury weddings to multinational launches, Swing The Boogie keeps audiences raving long after the final horn.";

        Testimonials = await _organizationContent.GetPublishedTestimonialsAsync().ConfigureAwait(false);
    }
}
