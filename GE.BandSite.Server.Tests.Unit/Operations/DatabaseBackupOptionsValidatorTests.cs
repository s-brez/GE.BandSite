using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Tests.Operations;

[TestFixture]
public class DatabaseBackupOptionsValidatorTests
{
    private DatabaseBackupOptionsValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new DatabaseBackupOptionsValidator();
    }

    [Test]
    public void Validate_WhenDisabled_AllowsMissingBucket()
    {
        var options = new DatabaseBackupOptions
        {
            Enabled = false,
            BucketName = string.Empty
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WhenEnabledWithoutBucket_Fails()
    {
        var options = new DatabaseBackupOptions
        {
            Enabled = true,
            BucketName = string.Empty,
            KeyPrefix = "backups/database"
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void Validate_WhenRetentionLessThanOne_Fails()
    {
        var options = new DatabaseBackupOptions
        {
            Enabled = true,
            BucketName = "ge-band-site-backups",
            KeyPrefix = "backups/database",
            RetentionDays = 0
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void Validate_WithValidOptions_Succeeds()
    {
        var options = new DatabaseBackupOptions
        {
            Enabled = true,
            BucketName = "ge-band-site-backups",
            KeyPrefix = "backups/database",
            RetentionDays = 30,
            PgDumpPath = "pg_dump"
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.That(result.Succeeded, Is.True);
    }
}
