using System.Globalization;
using System.Text;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Extensions;

namespace GE.BandSite.Server.Features.Contact;

public sealed class SesContactSubmissionNotifier : IContactSubmissionNotifier
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly IOptions<ContactNotificationOptions> _options;
    private readonly ILogger<SesContactSubmissionNotifier> _logger;

    public SesContactSubmissionNotifier(
        IAmazonSimpleEmailServiceV2 sesClient,
        IOptions<ContactNotificationOptions> options,
        ILogger<SesContactSubmissionNotifier> logger)
    {
        _sesClient = sesClient;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyAsync(ContactSubmissionNotification notification, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Skipping contact submission notification for {SubmissionId} because notifications are disabled.", notification.SubmissionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress) || string.IsNullOrWhiteSpace(options.ToAddress))
        {
            _logger.LogWarning("Contact submission notification skipped: sender or recipient not configured.");
            return;
        }

        var subject = string.IsNullOrWhiteSpace(options.Subject)
            ? "New Swing The Boogie contact submission"
            : options.Subject;

        var htmlBody = BuildHtmlBody(notification);
        var textBody = BuildTextBody(notification);

        var request = new SendEmailRequest
        {
            FromEmailAddress = options.FromAddress,
            Destination = new Destination
            {
                ToAddresses = new List<string> { options.ToAddress }
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content
                    {
                        Data = subject
                    },
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Data = htmlBody
                        },
                        Text = new Content
                        {
                            Data = textBody
                        }
                    }
                }
            }
        };

        await _sesClient.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent contact submission notification for {SubmissionId}.", notification.SubmissionId);
    }

    private static string BuildHtmlBody(ContactSubmissionNotification notification)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<h2>New Swing The Boogie enquiry</h2>");
        builder.AppendLine("<table style=\"border-collapse:collapse;width:100%;\">");
        AppendRow(builder, "Submitted", notification.CreatedAt.ToDateTimeUtc().ToString("u"));
        AppendRow(builder, "Organizer", notification.OrganizerName);
        AppendRow(builder, "Email", notification.OrganizerEmail);
        AppendRow(builder, "Phone", notification.OrganizerPhone);
        AppendRow(builder, "Event Type", notification.EventType);
        AppendRow(builder, "Event Date", FormatLocalDate(notification.EventDate));
        AppendRow(builder, "Location", notification.Location);
        AppendRow(builder, "Preferred Band Size", notification.PreferredBandSize);
        AppendRow(builder, "Budget Range", notification.BudgetRange);
        AppendRow(builder, "Message", notification.Message);
        builder.AppendLine("</table>");
        return builder.ToString();
    }

    private static string BuildTextBody(ContactSubmissionNotification notification)
    {
        var builder = new StringBuilder();
        builder.AppendLine("New Swing The Boogie enquiry");
        builder.AppendLine($"Submitted: {notification.CreatedAt.ToDateTimeUtc():u}");
        builder.AppendLine($"Organizer: {notification.OrganizerName}");
        builder.AppendLine($"Email: {notification.OrganizerEmail}");
        if (!string.IsNullOrWhiteSpace(notification.OrganizerPhone))
        {
            builder.AppendLine($"Phone: {notification.OrganizerPhone}");
        }
        builder.AppendLine($"Event Type: {notification.EventType}");
        var formattedDate = FormatLocalDate(notification.EventDate);
        if (!string.IsNullOrWhiteSpace(formattedDate))
        {
            builder.AppendLine($"Event Date: {formattedDate}");
        }
        if (!string.IsNullOrWhiteSpace(notification.Location))
        {
            builder.AppendLine($"Location: {notification.Location}");
        }
        builder.AppendLine($"Preferred Band Size: {notification.PreferredBandSize}");
        builder.AppendLine($"Budget Range: {notification.BudgetRange}");
        if (!string.IsNullOrWhiteSpace(notification.Message))
        {
            builder.AppendLine();
            builder.AppendLine("Message:");
            builder.AppendLine(notification.Message);
        }

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine("<tr>");
        builder.AppendLine($"<th style=\"text-align:left;padding:6px 10px;border-bottom:1px solid #333;\">{label}</th>");
        builder.AppendLine($"<td style=\"padding:6px 10px;border-bottom:1px solid #333;\">{System.Net.WebUtility.HtmlEncode(value)}</td>");
        builder.AppendLine("</tr>");
    }

    private static string? FormatLocalDate(LocalDate? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

public sealed class ContactNotificationOptions
{
    public bool Enabled { get; set; }

    public string? FromAddress { get; set; }

    public string? ToAddress { get; set; }

    public string? Subject { get; set; }
}
