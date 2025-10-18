using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Server.Features.Operations.Deliverability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GE.BandSite.Server.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/ses/notifications")]
public sealed class SesNotificationsController : ControllerBase
{
    private static readonly StringComparer ArnComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ISnsMessageValidator _validator;
    private readonly ISesNotificationProcessor _processor;
    private readonly SesWebhookOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SesNotificationsController> _logger;

    public SesNotificationsController(
        ISnsMessageValidator validator,
        ISesNotificationProcessor processor,
        IOptions<SesWebhookOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SesNotificationsController> logger)
    {
        _validator = validator;
        _processor = processor;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false))
        {
            body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("SES webhook received an empty payload.");
            return BadRequest();
        }

        SnsMessageEnvelope? envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<SnsMessageEnvelope>(body);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to deserialize SNS payload.");
            return BadRequest();
        }

        if (envelope == null)
        {
            _logger.LogWarning("SNS payload deserialized to null.");
            return BadRequest();
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("SES webhook disabled; payload ignored.");
            return Ok();
        }

        var isValid = await _validator.ValidateAsync(envelope, cancellationToken).ConfigureAwait(false);
        if (!isValid)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var messageType = envelope.Type?.Trim();
        if (string.IsNullOrEmpty(messageType))
        {
            _logger.LogWarning("SNS message rejected because Type was missing.");
            return BadRequest();
        }

        if (string.Equals(messageType, "SubscriptionConfirmation", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSubscriptionConfirmationAsync(envelope, cancellationToken).ConfigureAwait(false);
            return Ok();
        }

        if (string.Equals(messageType, "Notification", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(envelope.Message))
            {
                _logger.LogWarning("SNS notification ignored because Message was empty.");
                return BadRequest();
            }

            if (_options.RequireTopicValidation && !IsTopicAllowed(envelope.TopicArn))
            {
                _logger.LogWarning("SNS notification from TopicArn {TopicArn} rejected because it is not in the allowed list.", envelope.TopicArn);
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            SesNotificationMessage? notification;
            try
            {
                notification = JsonConvert.DeserializeObject<SesNotificationMessage>(envelope.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to deserialize SES notification payload.");
                return BadRequest();
            }

            if (notification == null)
            {
                _logger.LogWarning("SES notification payload deserialized to null.");
                return BadRequest();
            }

            await _processor.ProcessAsync(envelope, notification, envelope.Message, cancellationToken).ConfigureAwait(false);
            return Ok();
        }

        if (string.Equals(messageType, "UnsubscribeConfirmation", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received SNS unsubscribe confirmation for TopicArn {TopicArn}.", envelope.TopicArn);
            return Ok();
        }

        _logger.LogWarning("SNS message with unsupported type {Type} ignored.", messageType);
        return Ok();
    }

    private async Task HandleSubscriptionConfirmationAsync(SnsMessageEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!_options.AutoConfirmSubscriptions)
        {
            _logger.LogInformation("Subscription confirmation for TopicArn {TopicArn} ignored because auto-confirm is disabled.", envelope.TopicArn);
            return;
        }

        if (!Uri.TryCreate(envelope.SubscribeUrl, UriKind.Absolute, out var subscribeUri))
        {
            _logger.LogWarning("Subscription confirmation ignored because SubscribeURL {SubscribeUrl} was invalid.", envelope.SubscribeUrl);
            return;
        }

        if (!IsTrustedSnsUri(subscribeUri))
        {
            _logger.LogWarning("Subscription confirmation ignored because SubscribeURL {SubscribeUrl} was not trusted.", subscribeUri);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(SesNotificationsController));
            var response = await client.GetAsync(subscribeUri, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Confirmed SNS subscription for TopicArn {TopicArn}.", envelope.TopicArn);
            }
            else
            {
                _logger.LogWarning("Failed to confirm SNS subscription for TopicArn {TopicArn}. StatusCode={StatusCode}", envelope.TopicArn, response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error confirming SNS subscription for TopicArn {TopicArn}.", envelope.TopicArn);
        }
    }

    private bool IsTopicAllowed(string? topicArn)
    {
        if (string.IsNullOrWhiteSpace(topicArn))
        {
            return false;
        }

        var allowedTopics = GetAllowedTopics();
        return allowedTopics.Contains(topicArn);
    }

    private ISet<string> GetAllowedTopics()
    {
        var allowed = new HashSet<string>(ArnComparer);

        if (!string.IsNullOrWhiteSpace(_options.BounceTopicArn))
        {
            allowed.Add(_options.BounceTopicArn);
        }

        if (!string.IsNullOrWhiteSpace(_options.ComplaintTopicArn))
        {
            allowed.Add(_options.ComplaintTopicArn);
        }

        if (!string.IsNullOrWhiteSpace(_options.DeliveryTopicArn))
        {
            allowed.Add(_options.DeliveryTopicArn);
        }

        if (_options.AllowedTopicArns != null)
        {
            foreach (var arn in _options.AllowedTopicArns.Where(static a => !string.IsNullOrWhiteSpace(a)))
            {
                allowed.Add(arn);
            }
        }

        return allowed;
    }

    private static bool IsTrustedSnsUri(Uri uri)
    {
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.Host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase)
               && uri.Host.Contains(".sns.", StringComparison.OrdinalIgnoreCase);
    }
}
