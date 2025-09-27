using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Configuration;

public sealed class DatabaseBackupOptionsValidator : IValidateOptions<DatabaseBackupOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseBackupOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            return ValidateOptionsResult.Fail("DatabaseBackup:BucketName must be provided when backups are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyPrefix))
        {
            return ValidateOptionsResult.Fail("DatabaseBackup:KeyPrefix must be provided when backups are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            return ValidateOptionsResult.Fail("DatabaseBackup:ConnectionStringName must be provided when backups are enabled.");
        }

        if (options.RetentionDays < 1)
        {
            return ValidateOptionsResult.Fail("DatabaseBackup:RetentionDays must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(options.PgDumpPath))
        {
            return ValidateOptionsResult.Fail("DatabaseBackup:PgDumpPath must be provided when backups are enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
