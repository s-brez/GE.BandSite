using System.Net;
using System.Text.RegularExpressions;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using GE.BandSite.Database;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
[Explicit("Sends a live contact submission email through Amazon SES. Configure AWS_SES_* and CONTACT_NOTIFICATIONS_* environment variables before running.")]
[NonParallelizable]
public sealed class ContactSubmissionSesLiveTests
{
    private static readonly char[] RecipientSeparators = { '\r', '\n', ',', ';' };

    private TestPostgresProvider _postgres = null!;
    private LiveSesWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private IReadOnlyList<string> _recipients = Array.Empty<string>();
    private string _fromAddress = string.Empty;
    private string _subject = "New Swing The Boogie contact submission";

    [SetUp]
    public async Task SetUp()
    {
        var configuration = BuildConfiguration();

        try
        {
            _ = AwsConfiguration.FromConfiguration(configuration);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Ignore($"AWS configuration is incomplete: {exception.Message}");
            return;
        }

        _recipients = ParseRecipients(configuration["CONTACT_NOTIFICATIONS_RECIPIENTS"]);
        if (_recipients.Count == 0)
        {
            Assert.Ignore("CONTACT_NOTIFICATIONS_RECIPIENTS must contain at least one recipient email address.");
            return;
        }

        _fromAddress = configuration["CONTACT_NOTIFICATIONS_FROM_ADDRESS"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_fromAddress))
        {
            Assert.Ignore("CONTACT_NOTIFICATIONS_FROM_ADDRESS must be configured with a verified SES sender address.");
            return;
        }

        var subject = configuration["CONTACT_NOTIFICATIONS_SUBJECT"]?.Trim();
        if (!string.IsNullOrWhiteSpace(subject))
        {
            _subject = subject;
        }

        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _factory = new LiveSesWebApplicationFactory(_postgres, _fromAddress, _subject);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GeBandSiteDbContext>();
        await db.Database.EnsureCreatedAsync();

        var settings = scope.ServiceProvider.GetRequiredService<IContactNotificationSettingsService>();
        await settings.UpdateRecipientsAsync(_recipients, CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Test]
    public async Task SubmitContactForm_SendsLiveEmail()
    {
        var antiforgeryToken = await FetchAntiforgeryTokenAsync();

        var eventDateTime = DateTime.UtcNow.Date.AddDays(21).AddHours(18);
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.OrganizerName"] = "SES Integration Test",
            ["Input.OrganizerEmail"] = "integration-test@swingtheboogie.com",
            ["Input.OrganizerPhone"] = "+15551231234",
            ["Input.EventType"] = "Integration Test",
            ["Input.EventDateTime"] = eventDateTime.ToString("yyyy-MM-ddTHH:mm"),
            ["Input.EventTimezone"] = "Etc/UTC",
            ["Input.Location"] = "Test Lab",
            ["Input.PreferredBandSize"] = "Full Band",
            ["Input.Message"] = "Automated test submission to verify SES delivery."
        };

        var response = await _client.PostAsync("/Contact", new FormUrlEncodedContent(form));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Contact"));
        });

        await using var db = _postgres.CreateDbContext<GeBandSiteDbContext>();
        var stored = await db.ContactSubmissions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.OrganizerEmail, Is.EqualTo("integration-test@swingtheboogie.com"));
            Assert.That(stored.EventDate, Is.EqualTo(LocalDate.FromDateTime(eventDateTime.Date)));
            Assert.That(stored.EventTimezone, Is.EqualTo("Etc/UTC"));
            Assert.That(stored.BudgetRange, Is.EqualTo(ContactSubmissionDefaults.BudgetRangePlaceholder));
        });

        var sesClient = _factory.RecordingClient ?? throw new InvalidOperationException("SES client recorder was not initialized.");
        Assert.That(sesClient.Requests, Has.Count.EqualTo(1));
        Assert.That(sesClient.Responses, Has.Count.EqualTo(1));

        var request = sesClient.Requests.Single();
        var sendResponse = sesClient.Responses.Single();

        Assert.Multiple(() =>
        {
            Assert.That(request.FromEmailAddress, Is.EqualTo(_fromAddress));
            Assert.That(request.Destination?.ToAddresses, Is.EquivalentTo(_recipients));
            Assert.That(request.Content?.Simple?.Subject?.Data, Is.EqualTo(_subject));
            Assert.That(sendResponse.MessageId, Is.Not.Null.And.Not.Empty, "SES MessageId should be present for delivered emails.");
        });
    }

    private async Task<string> FetchAntiforgeryTokenAsync()
    {
        var response = await _client.GetAsync("/Contact");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new InvalidOperationException("Unable to locate antiforgery token on the contact page.");
        }

        return match.Groups[1].Value;
    }

    private static IReadOnlyList<string> ParseRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var validator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in raw.Split(RecipientSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            if (!validator.IsValid(candidate))
            {
                continue;
            }

            if (seen.Add(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static IConfiguration BuildConfiguration()
    {
        var serverProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GE.BandSite.Server"));

        return new ConfigurationBuilder()
            .SetBasePath(serverProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private sealed class LiveSesWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly TestPostgresProvider _postgres;
        private readonly string _fromAddress;
        private readonly string _subject;

        public LiveSesWebApplicationFactory(TestPostgresProvider postgres, string fromAddress, string subject)
        {
            _postgres = postgres;
            _fromAddress = fromAddress;
            _subject = subject;
        }

        public RecordingSesEmailClient? RecordingClient { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<GeBandSiteDbContext>));
                services.AddDbContext<GeBandSiteDbContext>(options =>
                    options.UseNpgsql(_postgres.ConnectionString, o => o.UseNodaTime()));
                services.AddScoped<IGeBandSiteDbContext>(sp => sp.GetRequiredService<GeBandSiteDbContext>());

                services.RemoveAll(typeof(ISesEmailClient));
                services.AddSingleton<ISesEmailClient>(sp =>
                {
                    var recorder = new RecordingSesEmailClient(sp.GetRequiredService<IAmazonSimpleEmailServiceV2>());
                    RecordingClient = recorder;
                    return recorder;
                });

                services.PostConfigure<ContactNotificationOptions>(options =>
                {
                    options.Enabled = true;
                    options.FromAddress = _fromAddress;
                    if (string.IsNullOrWhiteSpace(options.Subject))
                    {
                        options.Subject = _subject;
                    }
                });
            });
        }
    }

    private sealed class RecordingSesEmailClient : ISesEmailClient
    {
        private readonly Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2 _inner;

        public RecordingSesEmailClient(Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2 inner)
        {
            _inner = inner;
        }

        public List<SendEmailRequest> Requests { get; } = new();

        public List<SendEmailResponse> Responses { get; } = new();

        public async Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(Clone(request));
            var response = await _inner.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
            Responses.Add(response);
            return response;
        }

        private static SendEmailRequest Clone(SendEmailRequest request)
        {
            return new SendEmailRequest
            {
                FromEmailAddress = request.FromEmailAddress,
                Destination = request.Destination == null
                    ? null
                    : new Destination
                    {
                        ToAddresses = request.Destination.ToAddresses != null ? new List<string>(request.Destination.ToAddresses) : new List<string>()
                    },
                Content = request.Content
            };
        }
    }
}
