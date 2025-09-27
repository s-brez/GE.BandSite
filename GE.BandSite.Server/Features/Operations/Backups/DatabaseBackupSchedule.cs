namespace GE.BandSite.Server.Features.Operations.Backups;

public static class DatabaseBackupSchedule
{
    public static TimeSpan CalculateDelay(DateTimeOffset nowUtc, TimeOnly runAtUtc)
    {
        var todayRun = new DateTimeOffset(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            runAtUtc.Hour,
            runAtUtc.Minute,
            runAtUtc.Second,
            TimeSpan.Zero);

        if (nowUtc <= todayRun)
        {
            return todayRun - nowUtc;
        }

        var tomorrowRun = todayRun.AddDays(1);
        return tomorrowRun - nowUtc;
    }
}
