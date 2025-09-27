using System.ComponentModel;

namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Configures server logging behavior exposed via Serilog.
/// <para>
/// MinimumLevel controls the global minimum log event level. Valid values match Serilog's LogEventLevel names: Verbose, Debug, Information, Warning, Error, Fatal.
/// RetainedFileCount controls how many rolling log files to keep on disk.
/// </para>
/// </summary>
public sealed class LoggingConfiguration
{
    /// <summary>
    /// The minimum log level to emit. Defaults to Information.
    /// </summary>
    [DefaultValue("Information")]
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Number of rolling log files to retain.
    /// </summary>
    [DefaultValue(14)]
    public int RetainedFileCount { get; set; } = 14;
}

