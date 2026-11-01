using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using System;
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
        private static readonly TimeSpan s_MaxRetryDelay = TimeSpan.FromSeconds(60);

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
                    ShouldRetryAfterHeader = true,
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

        private static bool IsUpload(HttpRequestMessage request) =>
            request?.Method == HttpMethod.Post &&
            request.RequestUri?.AbsolutePath.TrimEnd('/') == "/v1/uploads";
    }
}
