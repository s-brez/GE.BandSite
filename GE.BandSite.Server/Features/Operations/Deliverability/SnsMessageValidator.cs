using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed class SnsMessageValidator : ISnsMessageValidator
{
    private static readonly ConcurrentDictionary<Uri, X509Certificate2> CertificateCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SnsMessageValidator> _logger;

    public SnsMessageValidator(IHttpClientFactory httpClientFactory, ILogger<SnsMessageValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(SnsMessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope == null)
        {
            return false;
        }

        if (!string.Equals(envelope.SignatureVersion, "1", StringComparison.Ordinal))
        {
            _logger.LogWarning("SNS message rejected because SignatureVersion {SignatureVersion} is unsupported.", envelope.SignatureVersion);
            return false;
        }

        if (!Uri.TryCreate(envelope.SigningCertUrl, UriKind.Absolute, out var certificateUri))
        {
            _logger.LogWarning("SNS message rejected because SigningCertURL {SigningCertUrl} is invalid.", envelope.SigningCertUrl);
            return false;
        }

        if (!IsTrustedCertificateEndpoint(certificateUri))
        {
            _logger.LogWarning("SNS message rejected because SigningCertURL {SigningCertUrl} is not trusted.", certificateUri);
            return false;
        }

        var certificate = await GetCertificateAsync(certificateUri, cancellationToken).ConfigureAwait(false);
        if (certificate == null)
        {
            _logger.LogWarning("SNS message rejected because certificate could not be resolved from {SigningCertUrl}.", certificateUri);
            return false;
        }

        var stringToSign = BuildStringToSign(envelope);
        if (stringToSign == null)
        {
            _logger.LogWarning("SNS message rejected because string to sign could not be constructed for type {Type}.", envelope.Type);
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Signature))
        {
            _logger.LogWarning("SNS message rejected because signature is missing.");
            return false;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException)
        {
            _logger.LogWarning("SNS message rejected because signature is not valid base64.");
            return false;
        }

        var data = Encoding.UTF8.GetBytes(stringToSign);
        RSA? rsa = null;
        try
        {
            rsa = certificate.GetRSAPublicKey();
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(exception, "SNS message rejected because certificate did not expose a usable RSA public key.");
            return false;
        }
        catch (PlatformNotSupportedException exception)
        {
            _logger.LogWarning(exception, "SNS message rejected because RSA public key retrieval is not supported on this platform.");
            return false;
        }

        if (rsa == null)
        {
            _logger.LogWarning("SNS message rejected because certificate did not contain an RSA public key.");
            return false;
        }

        try
        {
            var verified = rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            if (!verified)
            {
                _logger.LogWarning("SNS message rejected because signature verification failed.");
            }

            return verified;
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(exception, "SNS message rejected because signature verification threw an exception.");
            return false;
        }
        finally
        {
            rsa?.Dispose();
        }
    }

    private async Task<X509Certificate2?> GetCertificateAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (CertificateCache.TryGetValue(uri, out var cached))
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(nameof(SnsMessageValidator));
        client.Timeout = TimeSpan.FromSeconds(5);

        byte[] data;
        try
        {
            data = await client.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to download SNS signing certificate from {Uri}.", uri);
            return null;
        }

        try
        {
            var certificate = X509CertificateLoader.LoadCertificate(data);
            if (CertificateCache.TryAdd(uri, certificate))
            {
                return certificate;
            }

            return CertificateCache[uri];
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to parse SNS signing certificate from {Uri}.", uri);
            return null;
        }
    }

    private static string? BuildStringToSign(SnsMessageEnvelope envelope)
    {
        var type = envelope.Type;
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var builder = new StringBuilder();

        void Append(string label, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            builder.Append(label).Append('\n').Append(value).Append('\n');
        }

        Append("Message", envelope.Message);
        Append("MessageId", envelope.MessageId);

        if (string.Equals(type, "Notification", StringComparison.OrdinalIgnoreCase))
        {
            Append("Subject", envelope.Subject);
            Append("Timestamp", envelope.Timestamp);
            Append("TopicArn", envelope.TopicArn);
            Append("Type", envelope.Type);
        }
        else if (string.Equals(type, "SubscriptionConfirmation", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "UnsubscribeConfirmation", StringComparison.OrdinalIgnoreCase))
        {
            Append("SubscribeURL", envelope.SubscribeUrl);
            Append("Timestamp", envelope.Timestamp);
            Append("Token", envelope.Token);
            Append("TopicArn", envelope.TopicArn);
            Append("Type", envelope.Type);
        }
        else
        {
            return null;
        }

        return builder.ToString();
    }

    private static bool IsTrustedCertificateEndpoint(Uri uri)
    {
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host;
        if (!host.EndsWith(".amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!host.StartsWith("sns.", StringComparison.OrdinalIgnoreCase) &&
            !host.Contains(".sns.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
