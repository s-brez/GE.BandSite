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
    private readonly ISesEmailClient _sesClient;
    private readonly IOptions<ContactNotificationOptions> _options;
    private readonly IContactNotificationRecipientProvider _recipientProvider;
    private readonly ILogger<SesContactSubmissionNotifier> _logger;

    public SesContactSubmissionNotifier(
        ISesEmailClient sesClient,
        IOptions<ContactNotificationOptions> options,
        IContactNotificationRecipientProvider recipientProvider,
        ILogger<SesContactSubmissionNotifier> logger)
    {
        _sesClient = sesClient;
        _options = options;
        _recipientProvider = recipientProvider;
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

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            _logger.LogWarning("Contact submission notification skipped: sender address not configured.");
            return;
        }

        var recipients = await ResolveRecipientsAsync(options, cancellationToken).ConfigureAwait(false);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Contact submission notification skipped: no recipients configured.");
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
                ToAddresses = new List<string>(recipients)
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

        var response = await _sesClient.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Sent contact submission notification for {SubmissionId}. SES MessageId: {MessageId}.",
            notification.SubmissionId,
            response.MessageId);
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(ContactNotificationOptions options, CancellationToken cancellationToken)
    {
        var recipients = await _recipientProvider.GetRecipientEmailsAsync(cancellationToken).ConfigureAwait(false);
        if (recipients.Count > 0)
        {
            return recipients;
        }

        if (options.ToAddresses.Count == 0)
        {
            return Array.Empty<string>();
        }

        var fallback = options.ToAddresses
            .Where(static address => !string.IsNullOrWhiteSpace(address))
            .Select(static address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return fallback.Length == 0 ? Array.Empty<string>() : fallback;
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
        AppendRow(builder, "Event Timezone", notification.EventTimezone);
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
        if (!string.IsNullOrWhiteSpace(notification.EventTimezone))
        {
            builder.AppendLine($"Event Timezone: {notification.EventTimezone}");
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

    public List<string> ToAddresses { get; set; } = new();

    public string? Subject { get; set; }
}

public interface ISesEmailClient
{
    Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
}

public sealed class SesEmailClient : ISesEmailClient
{
    private readonly IAmazonSimpleEmailServiceV2 _client;

    public SesEmailClient(IAmazonSimpleEmailServiceV2 client)
    {
        _client = client;
    }

    public Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        return _client.SendEmailAsync(request, cancellationToken);
    }
}
