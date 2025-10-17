using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Services;

/// <summary>
/// Details Swing The Boogie service offerings and lineup packages.
/// </summary>
public class IndexModel : PageModel
{
    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroLead { get; private set; } = string.Empty;

    public IReadOnlyList<ServicePackage> ServicePackages { get; private set; } = Array.Empty<ServicePackage>();

    public IReadOnlyList<LineupPackage> LineupPackages { get; private set; } = Array.Empty<LineupPackage>();

    public IReadOnlyList<string> AddOns { get; private set; } = Array.Empty<string>();

    public void OnGet()
    {
        HeroTitle = "Turnkey swing experiences to make your event special";
        HeroLead = "";

        ServicePackages = new List<ServicePackage>
        {
            new(
                "Corporate Events",
                "Launch parties, awards nights, and incentive trips that demand high-gloss entertainment.",
                new List<string>
                {

                }),
            new(
                "Weddings",
                "From the aisle to the afterparty, Swing The Boogie provides the soundtrack to your perfect day. Ceremony ballads, cocktail jazz, dance-floor anthems � we�ve got every moment covered.",
                new List<string>
                {

                }),
            new(
                "Private Functions",
                "LCelebrate life�s milestones with music that lifts the room. Birthdays, anniversaries, festivals � our band transforms gatherings into unforgettable nights.",
                new List<string>
                {

                }),
        };

        LineupPackages = new List<LineupPackage>
        {
            new("Solo / Duo", "Perfect for ceremonies and small functions."),
            new("5-Piece", "Cocktail and dinner events."),
            new("10-Piece", "Full big band experience."),
        };

        AddOns = new List<string>
        {
            "Live brass fanfare for grand entrances and award reveals.",
            "Custom charts for first dances or signature brand moments.",
            "Let us know what you have in mind... Just ask!",
        };
    }

    public sealed record ServicePackage(string Name, string Summary, IReadOnlyList<string> Highlights);

    public sealed record LineupPackage(string Name, string Description);
}
