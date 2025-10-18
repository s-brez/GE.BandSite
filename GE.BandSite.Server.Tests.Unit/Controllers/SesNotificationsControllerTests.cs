using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Server.Controllers;
using GE.BandSite.Server.Features.Operations.Deliverability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Tests.Unit.Controllers;

[TestFixture]
public sealed class SesNotificationsControllerTests
{
    [Test]
    public async Task PostAsync_InvalidSignature_ReturnsForbid()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        await using var provider = services.BuildServiceProvider();

        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var validator = new SnsMessageValidator(httpClientFactory, NullLogger<SnsMessageValidator>.Instance);
        var options = Options.Create(new SesWebhookOptions
        {
            Enabled = true,
            AutoConfirmSubscriptions = false,
            RequireTopicValidation = false
        });

        var controller = new SesNotificationsController(
            validator,
            new NullProcessor(),
            options,
            httpClientFactory,
            NullLogger<SesNotificationsController>.Instance);

        var context = new DefaultHttpContext();
        var payload = """
            {
              "Type": "SubscriptionConfirmation",
              "MessageId": "16554545",
              "Token": "2336412",
              "TopicArn": "arn:aws:sns:ap-southeast-2:123456789012:ses-bounce",
              "Message": "test",
              "SubscribeURL": "https://sns.ap-southeast-2.amazonaws.com/?Action=ConfirmSubscription&TopicArn=arn:aws:sns:ap-southeast-2:123456789012:ses-bounce&Token=2336412",
              "SignatureVersion": "1",
              "Signature": "fake",
              "SigningCertURL": "https://sns.ap-southeast-2.amazonaws.com/cert.pem",
              "Timestamp": "2025-10-18T04:05:00.000Z"
            }
            """;

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        var result = await controller.PostAsync(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<StatusCodeResult>());
        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    private sealed class NullProcessor : ISesNotificationProcessor
    {
        public Task ProcessAsync(SnsMessageEnvelope envelope, SesNotificationMessage notification, string rawMessage, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
