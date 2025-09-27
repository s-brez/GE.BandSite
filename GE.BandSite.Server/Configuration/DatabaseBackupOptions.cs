using System.ComponentModel;

namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Configures nightly PostgreSQL backups executed via <c>pg_dump</c> and stored in S3.
/// <para>
/// When <see cref="Enabled"/> is true the service schedules a backup run every day at <see cref="RunAtUtc"/>,
/// uploads the resulting archive to <see cref="BucketName"/>/<see cref="KeyPrefix"/>, and prunes objects older than <see cref="RetentionDays"/>.
/// </para>
/// </summary>
public sealed class DatabaseBackupOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the nightly backup hosted service should run.
    /// </summary>
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time of day when the backup should execute.
    /// </summary>
    [DefaultValue(typeof(TimeOnly), "03:00:00")]
    public TimeOnly RunAtUtc { get; set; } = new(3, 0, 0);

    /// <summary>
    /// Gets or sets the name of the connection string used for pg_dump. Defaults to the primary database connection.
    /// </summary>
    [DefaultValue("Database")]
    public string ConnectionStringName { get; set; } = "Database";

    /// <summary>
    /// Gets or sets the number of days to retain database backups in S3.
    /// </summary>
    [DefaultValue(30)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the S3 bucket that stores database backups.
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 key prefix used to organize database backups.
    /// </summary>
    [DefaultValue("backups/database")]
    public string KeyPrefix { get; set; } = "backups/database";

    /// <summary>
    /// Gets or sets the path to the pg_dump executable.
    /// </summary>
    [DefaultValue("pg_dump")]
    public string PgDumpPath { get; set; } = "pg_dump";

    /// <summary>
    /// Gets or sets a directory that temporarily stores the generated dump prior to upload.
    /// When empty the application will fall back to a subdirectory of the application base path.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}
