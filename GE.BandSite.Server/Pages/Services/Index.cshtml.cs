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
        HeroTitle = "Turnkey swing experiences for every milestone.";
        HeroLead = "From Fortune 100 galas to sunset ceremonies, Swing The Boogie brings concierge-level planning with electrifying performance.";

        ServicePackages = new List<ServicePackage>
        {
            new(
                "Corporate Events",
                "Launch parties, awards nights, and incentive trips that demand high-gloss entertainment.",
                new List<string>
                {
                    "Custom walk-on stings and branded medleys.",
                    "MC services and run-sheet partnership with production teams.",
                    "Curated sets to match networking flows, dinners, and dance blocks.",
                }),
            new(
                "Weddings",
                "Ceremony to last dance with everything from string serenades to roaring horn finales.",
                new List<string>
                {
                    "Processional arrangements, first dance customization, and family dedications.",
                    "Seamless transitions between cocktail, dinner, and dance segments.",
                    "Timeline coordination with planners, celebrants, and AV partners.",
                }),
            new(
                "Private Functions",
                "Luxury birthdays, anniversaries, and society gatherings seeking decadent swing.",
                new List<string>
                {
                    "Ambient dinner sets with jazz standards and modern twists.",
                    "Audience participation moments and encore medleys tailored to guests.",
                    "Travel-ready crew for villas, yachts, and destination venues.",
                }),
        };

        LineupPackages = new List<LineupPackage>
        {
            new("Solo / Duo", "Perfect for ceremonies, cocktail lounges, and refined dinners where intimacy matters."),
            new("5-Piece Ensemble", "Upright bass, drums, guitar, keys, and vocals delivering vintage swing with a modern pulse."),
            new("10-Piece Showcase", "Horn section, dual vocals, and rhythm extras for spectacular stages and festival energy."),
        };

        AddOns = new List<string>
        {
            "Live brass fanfare for grand entrances and award reveals.",
            "DJ + horn hybrid after-party sets.",
            "Custom charts for first dances or signature brand moments.",
            "International logistics coordination including carnet prep and backline specs.",
        };
    }

    public sealed record ServicePackage(string Name, string Summary, IReadOnlyList<string> Highlights);

    public sealed record LineupPackage(string Name, string Description);
}
