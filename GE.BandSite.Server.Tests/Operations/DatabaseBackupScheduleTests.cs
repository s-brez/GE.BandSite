using GE.BandSite.Server.Features.Operations.Backups;

namespace GE.BandSite.Server.Tests.Operations;

[TestFixture]
public class DatabaseBackupScheduleTests
{
    [Test]
    public void CalculateDelay_WhenNowBeforeRunTime_ComputesForwardDelay()
    {
        var now = new DateTimeOffset(2025, 5, 5, 1, 0, 0, TimeSpan.Zero);
        var runAt = new TimeOnly(3, 0);

        var delay = DatabaseBackupSchedule.CalculateDelay(now, runAt);

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void CalculateDelay_WhenNowAfterRunTime_SchedulesNextDay()
    {
        var now = new DateTimeOffset(2025, 5, 5, 6, 30, 0, TimeSpan.Zero);
        var runAt = new TimeOnly(3, 0);

        var delay = DatabaseBackupSchedule.CalculateDelay(now, runAt);

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(20.5)));
    }

    [Test]
    public void CalculateDelay_WhenExactRunTime_ReturnsZero()
    {
        var now = new DateTimeOffset(2025, 5, 5, 3, 0, 0, TimeSpan.Zero);
        var runAt = new TimeOnly(3, 0);

        var delay = DatabaseBackupSchedule.CalculateDelay(now, runAt);

        Assert.That(delay, Is.EqualTo(TimeSpan.Zero));
    }
}
