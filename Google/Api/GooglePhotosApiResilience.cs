using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace GooglePhotoSync.Google.Api
{
    /// <summary>
    /// Resilience (retry + timeout) for the Google Photos API HttpClient.
    /// </summary>
    public static class GooglePhotosApiResilience
    {
        private static readonly TimeSpan s_AttemptTimeout = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan s_RetryBaseDelay = TimeSpan.FromSeconds(2);
        // Google requires at least 30s before retrying a 429, with exponential backoff from there.
        private static readonly TimeSpan s_TooManyRequestsBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan s_TooManyRequestsMaxJitter = TimeSpan.FromSeconds(5);
        // Upper bound for every retry delay, including 429 backoff and Retry-After. Enforced in GetRetryDelay rather than
        // relying on Polly's MaxDelay, so the backstop timeout calculation is exact.
        private static readonly TimeSpan s_MaxRetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Adds a resilience handler that retries transient upload failures and applies a per-attempt timeout to all calls.
        /// Register it before any handler that should run on every attempt (e.g. auth), so that it is the outer handler.
        /// </summary>
        public static IHttpClientBuilder AddGooglePhotosResilience(this IHttpClientBuilder builder, int uploadRetries)
        {
            builder.AddResilienceHandler("google-photos", (pipeline, context) => ConfigurePipeline(pipeline, context, uploadRetries));
            return builder;
        }

        /// <summary>
        /// The real timeout is per-attempt in the resilience pipeline. HttpClient.Timeout spans all retries, so this is only
        /// a backstop - e.g. for a response body that stalls after headers, which the pipeline timeout doesn't cover.
        /// It is the worst case for a full retry sequence (every attempt timing out, every wait at the max delay) plus a margin,
        /// so it never cuts off legitimate retries.
        /// </summary>
        public static TimeSpan GetHttpClientBackstopTimeout(int uploadRetries)
        {
            var retries = Math.Max(uploadRetries, 0);
            return s_AttemptTimeout * (retries + 1) + s_MaxRetryDelay * retries + TimeSpan.FromMinutes(1);
        }

        private static void ConfigurePipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, ResilienceHandlerContext context, int uploadRetries)
        {
            // Retry only media uploads. Other POSTs (batchCreate, create album) aren't idempotent - a retry could create duplicates.
            if (uploadRetries > 0)
            {
                var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(GooglePhotosApiResilience));

                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = uploadRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = s_RetryBaseDelay,
                    MaxDelay = s_MaxRetryDelay,
                    // Must be set before DelayGenerator: its setter replaces DelayGenerator. Retry-After is handled in GetRetryDelay.
                    ShouldRetryAfterHeader = false,
                    DelayGenerator = args => ValueTask.FromResult(GetRetryDelay(args.Outcome.Result, args.AttemptNumber)),
                    // 408, 429, 5xx, HttpRequestException and per-attempt timeouts - but only for uploads.
                    ShouldHandle = args => ValueTask.FromResult(IsUpload(args.Context.GetRequestMessage()) &&
                                                                HttpClientResiliencePredicates.IsTransient(args.Outcome)),
                    OnRetry = args =>
                    {
                        var error = args.Outcome.Result is { } response
                                        ? $"{(int)response.StatusCode} {response.StatusCode}"
                                        : args.Outcome.Exception?.Message;
                        logger.LogWarning("Transient upload failure (attempt {attempt} of {total}): {error}. Retrying in {delay:0.0}s.",
                                          args.AttemptNumber + 1, uploadRetries + 1, error, args.RetryDelay.TotalSeconds);
                        return ValueTask.CompletedTask;
                    }
                });
            }

            // Per attempt, so a slow upload that times out can still be retried.
            pipeline.AddTimeout(s_AttemptTimeout);
        }

        /// <summary>
        /// Delay before the next retry, or null to use the default exponential backoff (2s base, with jitter).
        /// - 429: at least 30s, doubling per attempt (30s, 60s, 120s...) plus jitter, or Retry-After if that is longer.
        /// - Other responses: Retry-After if Google sent one.
        /// All delays are capped at s_MaxRetryDelay.
        /// </summary>
        private static TimeSpan? GetRetryDelay(HttpResponseMessage response, int attemptNumber)
        {
            var retryAfter = GetRetryAfter(response);

            if (response?.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var backoff = s_TooManyRequestsBaseDelay * Math.Pow(2, attemptNumber)
                            + s_TooManyRequestsMaxJitter * Random.Shared.NextDouble();
                return Cap(retryAfter is { } ra && ra > backoff ? ra : backoff);
            }

            return retryAfter is { } delay ? Cap(delay) : null;
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            if (response?.Headers.RetryAfter is not { } retryAfter)
                return null;

            var delay = retryAfter.Delta ?? (retryAfter.Date - DateTimeOffset.UtcNow);
            return delay > TimeSpan.Zero ? delay : null;
        }

        private static TimeSpan Cap(TimeSpan delay) => delay < s_MaxRetryDelay ? delay : s_MaxRetryDelay;

        /// <summary>
        /// Raw uploads and the resumable 'start' / 'query' commands, which are safe to resend.
        /// Resumable chunk requests ('upload' / 'upload, finalize') are excluded: after a failure Google may have stored part
        /// of the chunk, so the upload must continue from the offset it reports. GooglePhotosUploader handles that.
        /// </summary>
        private static bool IsUpload(HttpRequestMessage request) =>
            request?.Method == HttpMethod.Post &&
            request.RequestUri?.AbsolutePath.TrimEnd('/') == "/v1/uploads" &&
            !IsResumableChunk(request);

        private static bool IsResumableChunk(HttpRequestMessage request) =>
            request.Headers.TryGetValues("X-Goog-Upload-Command", out var commands) &&
            commands.Any(c => c.Contains("upload", StringComparison.OrdinalIgnoreCase));
    }
}
