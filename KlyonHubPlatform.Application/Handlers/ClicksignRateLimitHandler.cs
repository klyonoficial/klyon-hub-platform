using System.Net;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Handlers
{
    /// <summary>
    /// DelegatingHandler que trata HTTP 429 (Too Many Requests) da Clicksign, aguardando até X-Rate-Limit-Reset e reenviando a requisição.
    /// Documentação: https://developers.clicksign.com/docs/limite-de-requisicoes
    /// </summary>
    public class ClicksignRateLimitHandler : DelegatingHandler
    {
        /// <summary>Header com o horário (Unix UTC) em que o limite será reiniciado.</summary>
        public const string HeaderRateLimitReset = "X-Rate-Limit-Reset";

        private readonly ILogger<ClicksignRateLimitHandler> _logger;
        private readonly int _maxRetries;

        public ClicksignRateLimitHandler(ILogger<ClicksignRateLimitHandler> logger, int maxRetries = 3)
        {
            _logger = logger;
            _maxRetries = maxRetries > 0 ? maxRetries : 3;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[]? contentBytes = null;
            string? contentType = null;
            if (request.Content != null)
            {
                contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                contentType = request.Content.Headers.ContentType?.ToString();
                request.Content = new ByteArrayContent(contentBytes);
                if (!string.IsNullOrEmpty(contentType))
                    request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            }

            var attempt = 0;
            while (true)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _maxRetries)
                    return response;

                if (!response.Headers.TryGetValues(HeaderRateLimitReset, out var resetValues))
                {
                    _logger.LogWarning("Clicksign retornou 429 sem header X-Rate-Limit-Reset. Aguardando 11 segundos antes de retry.");
                    await Task.Delay(TimeSpan.FromSeconds(11), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var resetStr = resetValues.FirstOrDefault();
                    if (long.TryParse(resetStr, out var resetUnix))
                    {
                        var resetUtc = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                        var delay = resetUtc - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                        {
                            _logger.LogInformation(
                                "Clicksign rate limit (429). Aguardando até {ResetUtc} ({DelaySeconds}s) antes de retry {Attempt}/{Max}.",
                                resetUtc.UtcDateTime, delay.TotalSeconds, attempt + 1, _maxRetries);
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(11), cancellationToken).ConfigureAwait(false);
                    }
                }

                response.Dispose();
                request = CloneRequest(request, contentBytes, contentType);
                attempt++;
            }
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? contentBytes, string? contentType)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri);
            foreach (var header in original.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (contentBytes != null)
            {
                clone.Content = new ByteArrayContent(contentBytes);
                if (!string.IsNullOrEmpty(contentType))
                    clone.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            }
            return clone;
        }
    }
}
