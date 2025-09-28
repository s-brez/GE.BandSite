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
        HeroTitle = "Speakeasy roots to global stages";
        HeroSubtitle = "Gilbert Ernest leads Swing The Boogie with a modern swagger that keeps the golden age of swing alive for today’s audiences.";

        StoryParagraphs = new List<string>
        {
            "Every event deserves an atmosphere. With a unique lineup of ten highly skilled musicians, we adapt to suit the occasion – from a soulful singer at your ceremony to a roaring big band for your gala dinner.",
            "We combine global polish with local passion, ensuring your guests are talking about the music long after the night ends."
        };

        WhyChooseUsPoints = new List<string>
        {
            "Ten world-class musicians rotating across horns, rhythm, and vocals to match any event footprint.",
            "Fly-in ready, bringing utmost professionalism and engaging energy.",
            "Gilbert Ernest anchors with charismatic emceeing, custom medleys, and on-the-fly shaping of the crowd.",
        };

        SpotlightFacts = new List<SpotlightFact>
        {
            new("120+", "International performances over the last five years."),
            new("1-10 Piece", "Configurable lineup covering solo piano through full horn section."),
            new("5+ years ", "From Monaco galas to Singapore rooftop soirées since 2019."),
        };

        ShowcaseImages = new List<ShowcaseImage>
        {
            new("/images/about-stage.svg", "Swing The Boogie playing a roaring theatre"),
            new("/images/about-duo.svg", "Gilbert Ernest with a vocalist in an intimate lounge"),
        };
    }

    public sealed record SpotlightFact(string Heading, string Detail);

    public sealed record ShowcaseImage(string Path, string AltText);
}
