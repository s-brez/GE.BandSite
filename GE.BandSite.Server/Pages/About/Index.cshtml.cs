using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.About;

/// <summary>
/// Presents the Swing The Boogie story and differentiators.
/// </summary>
public class IndexModel : PageModel
{
    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroSubtitle { get; private set; } = string.Empty;

    public IReadOnlyList<string> StoryParagraphs { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<string> WhyChooseUsPoints { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<SpotlightFact> SpotlightFacts { get; private set; } = Array.Empty<SpotlightFact>();

    public IReadOnlyList<ShowcaseImage> ShowcaseImages { get; private set; } = Array.Empty<ShowcaseImage>();

    public void OnGet()
    {
        HeroTitle = "From speakeasy roots to global stages.";
        HeroSubtitle = "Gilbert Ernest leads Swing The Boogie with a modern swagger that keeps the golden age of swing alive for today’s audiences.";

        StoryParagraphs = new List<string>
        {
            "Swing The Boogie got its start in Chicago supper clubs, where Gilbert Ernest assembled a crew of brass, rhythm, and strings who could turn any room into a dance hall.",
            "A decade later we are performing across five continents for luxury weddings, Fortune 100 brand summits, and high-energy festivals. Wherever we set up, the dance floor fills within the first chorus.",
            "The band flexes between intimate trio sets and roaring ten-piece showcases, carrying professional production values and concierge-level planning to every event.",
        };

        WhyChooseUsPoints = new List<string>
        {
            "Ten world-class musicians rotating across horns, rhythm, and vocals to match any event footprint.",
            "Fly-in ready with modular backline, liaising directly with AV teams to deliver seamless changeovers.",
            "Gilbert Ernest anchors the performance with charismatic emceeing, custom medleys, and on-the-fly set shaping for crowd response.",
        };

        SpotlightFacts = new List<SpotlightFact>
        {
            new("120+", "International performances over the last five years."),
            new("1–10 Piece", "Configurable lineup covering solo piano through full horn section."),
            new("5 Continents", "From Monaco galas to Singapore rooftop soirées."),
        };

        ShowcaseImages = new List<ShowcaseImage>
        {
            new("~/images/about-stage.svg", "Swing The Boogie playing a roaring theatre"),
            new("~/images/about-duo.svg", "Gilbert Ernest with a vocalist in an intimate lounge"),
        };
    }

    public sealed record SpotlightFact(string Heading, string Detail);

    public sealed record ShowcaseImage(string Path, string AltText);
}
