namespace GE.BandSite.Server.Features.Contact;

/// <summary>
/// Provides default values applied to contact submissions when customers are not prompted
/// to supply specific details during the initial enquiry flow.
/// </summary>
public static class ContactSubmissionDefaults
{
    /// <summary>
    /// Placeholder budget range stored with the submission until the bookings team confirms
    /// details directly with the customer.
    /// </summary>
    public const string BudgetRangePlaceholder = "Budget to be discussed";
}
