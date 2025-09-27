using System.ComponentModel;

namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Configuration controlling optional HTTP request logging. Disabled by default.
/// </summary>
public sealed class RequestLoggingConfiguration
{
    /// <summary>
    /// When true, enables Serilog request logging middleware.
    /// </summary>
    [DefaultValue(false)]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true, includes basic header details (User-Agent, Client IP, Referer) in log properties.
    /// </summary>
    [DefaultValue(false)]
    public bool IncludeHeaders { get; set; } = false;

    /// <summary>
    /// When true (default), masks public tracking tokens found in request paths and Referer.
    /// </summary>
    [DefaultValue(true)]
    public bool MaskPublicTrackingTokens { get; set; } = true;
}

