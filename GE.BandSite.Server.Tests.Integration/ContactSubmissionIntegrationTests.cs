using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Amazon.SimpleEmailV2.Model;
using GE.BandSite.Database;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class ContactSubmissionIntegrationTests
{
    private TestPostgresProvider _postgres = null!;
    private ContactWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _factory = new ContactWebApplicationFactory(_postgres);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GeBandSiteDbContext>();
        await db.Database.EnsureCreatedAsync();

        var settings = scope.ServiceProvider.GetRequiredService<IContactNotificationSettingsService>();
        await settings.UpdateRecipientsAsync(new[] { "sam@sdbgrop.io" }, CancellationToken.None);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_client != null)
        {
            _client.Dispose();
        }

        if (_factory != null)
        {
            _factory.Dispose();
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Test]
    public async Task PostContactForm_PersistsSubmissionAndSendsNotification()
    {
        var token = await FetchAntiforgeryTokenAsync();

        var eventDateTime = DateTime.UtcNow.Date.AddDays(45).AddHours(19);

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.OrganizerName"] = "Jordan Hart",
            ["Input.OrganizerEmail"] = "jordan@example.com",
            ["Input.OrganizerPhone"] = "+13125550191",
            ["Input.EventType"] = "Corporate Event",
            ["Input.EventDateTime"] = eventDateTime.ToString("yyyy-MM-ddTHH:mm"),
            ["Input.EventTimezone"] = "Australia/Sydney",
            ["Input.Location"] = "Chicago, IL",
            ["Input.PreferredBandSize"] = "10-Piece",
            ["Input.BudgetRange"] = "40k+",
            ["Input.Message"] = "Need horn feature for award reveal."
        };

        var response = await _client.PostAsync("/Contact", new FormUrlEncodedContent(form));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Contact"));

        await using var db = _postgres.CreateDbContext<GeBandSiteDbContext>();
        var stored = await db.ContactSubmissions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.OrganizerEmail, Is.EqualTo("jordan@example.com"));
            Assert.That(stored.EventType, Is.EqualTo("Corporate Event"));
            Assert.That(stored.EventDate, Is.EqualTo(LocalDate.FromDateTime(eventDateTime.Date)));
            Assert.That(stored.EventTimezone, Is.EqualTo("Australia/Sydney"));
        });

        Assert.That(_factory.SesClient.Requests, Has.Count.EqualTo(1));
        var request = _factory.SesClient.Requests.Single();

        Assert.Multiple(() =>
        {
            Assert.That(request.FromEmailAddress, Is.EqualTo("notifications@swingtheboogie.com"));
            Assert.That(request.Destination.ToAddresses, Is.EquivalentTo(new[] { "sam@sdbgrop.io" }));
            Assert.That(request.Content?.Simple?.Subject?.Data, Is.EqualTo("New Swing The Boogie contact submission"));
            Assert.That(request.Content?.Simple?.Body?.Html?.Data, Does.Contain("Jordan Hart"));
        });
    }

    [Test]
    public async Task GetAdminContactSubmissions_WithoutAuth_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Admin/ContactSubmissions");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo("/Login"));
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
            throw new InvalidOperationException("Antiforgery token not found in contact page.");
        }

        return match.Groups[1].Value;
    }

    private sealed class ContactWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly TestPostgresProvider _postgres;

        public ContactWebApplicationFactory(TestPostgresProvider postgres)
        {
            _postgres = postgres;
            SesClient = new FakeSesEmailClient();
        }

        public FakeSesEmailClient SesClient { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<GeBandSiteDbContext>));
                services.AddDbContext<GeBandSiteDbContext>(options =>
                    options.UseNpgsql(_postgres.ConnectionString, o => o.UseNodaTime()));
                services.AddScoped<IGeBandSiteDbContext>(sp => sp.GetRequiredService<GeBandSiteDbContext>());

                services.RemoveAll(typeof(ISesEmailClient));
                services.AddSingleton<ISesEmailClient>(SesClient);

                services.PostConfigure<ContactNotificationOptions>(options =>
                {
                    options.Enabled = true;
                    options.FromAddress = "notifications@swingtheboogie.com";
                    options.Subject = "New Swing The Boogie contact submission";
                });
            });
        }
    }

    private sealed class FakeSesEmailClient : ISesEmailClient
    {
        public List<SendEmailRequest> Requests { get; } = new();

        public Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(Clone(request));
            return Task.FromResult(new SendEmailResponse
            {
                MessageId = Guid.NewGuid().ToString()
            });
        }

        private static SendEmailRequest Clone(SendEmailRequest request)
        {
            return new SendEmailRequest
            {
                FromEmailAddress = request.FromEmailAddress,
                Destination = new Destination
                {
                    ToAddresses = request.Destination?.ToAddresses != null ? new List<string>(request.Destination.ToAddresses) : new List<string>()
                },
                Content = request.Content
            };
        }
    }
}
