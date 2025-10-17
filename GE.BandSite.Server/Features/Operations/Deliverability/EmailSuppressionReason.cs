namespace GE.BandSite.Server.Features.Operations.Deliverability;

public static class EmailSuppressionReason
{
    public const string PermanentBounce = "PermanentBounce";
    public const string UndeterminedBounce = "UndeterminedBounce";
    public const string Complaint = "Complaint";
    public const string Manual = "Manual";
}
