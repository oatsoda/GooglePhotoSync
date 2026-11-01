using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GooglePhotoSync.Google
{
    /// <summary>
    /// Uploads media bytes to Google Photos and returns an upload token for mediaItems:batchCreate.
    /// Files up to ResumableUploadThresholdMB use a single raw upload; larger files use the resumable (chunked) protocol:
    /// https://developers.google.com/photos/library/guides/resumable-uploads
    /// </summary>
    /// <remarks>
    /// Typed HttpClient sharing the Google Photos client configuration (base address, resilience pipeline, auth handler).
    /// The resilience pipeline retries the raw upload and the resumable start/query requests. Chunk requests are excluded
    /// because a failed chunk must be resumed from the offset Google reports, not resent - that is handled here.
    /// </remarks>
    public class GooglePhotosUploader
    {
        private const string UploadsPath = "v1/uploads";
        private const long DefaultChunkGranularity = 256 * 1024;

        private static readonly TimeSpan s_ResumeBaseDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan s_TooManyRequestsBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan s_MaxResumeDelay = TimeSpan.FromMinutes(2);

        private readonly HttpClient m_HttpClient;
        private readonly IGooglePhotosApi m_GooglePhotosApi;
        private readonly GoogleSettings m_GoogleSettings;
        private readonly int m_MaxResumes;
        private readonly ILogger<GooglePhotosUploader> m_Logger;

        public GooglePhotosUploader(HttpClient httpClient,
                                    IGooglePhotosApi googlePhotosApi,
                                    IOptions<GoogleSettings> googleSettings,
                                    IOptions<SyncSettings> syncSettings,
                                    ILogger<GooglePhotosUploader> logger)
        {
            m_HttpClient = httpClient;
            m_GooglePhotosApi = googlePhotosApi;
            m_GoogleSettings = googleSettings.Value;
            m_MaxResumes = syncSettings.Value.UploadRetries;
            m_Logger = logger;

            if (m_GoogleSettings.ResumableUploadThresholdMB < 0)
                throw new InvalidOperationException($"{nameof(GoogleSettings.ResumableUploadThresholdMB)} must not be negative.");

            if (m_GoogleSettings.ResumableUploadChunkSizeMB <= 0)
                throw new InvalidOperationException($"{nameof(GoogleSettings.ResumableUploadChunkSizeMB)} must be greater than 0.");
        }

        public async Task<string> Upload(LocalFile file, CancellationToken cancellationToken = default)
        {
            if (file.Bytes <= m_GoogleSettings.ResumableUploadThresholdBytes)
            {
                // Transient failures are retried by the resilience handler. The FileStream is seekable, so StreamContent rewinds it for each attempt.
                using var stream = file.OpenStream();
                return await m_GooglePhotosApi.UploadFile(stream, file.MimeType);
            }

            return await ResumableUpload(file, cancellationToken);
        }

        private async Task<string> ResumableUpload(LocalFile file, CancellationToken cancellationToken)
        {
            var (sessionUrl, granularity) = await StartSession(file, cancellationToken);

            // Every chunk except the last must be a multiple of the granularity.
            var chunkSize = Math.Max(granularity, m_GoogleSettings.ResumableUploadChunkSizeBytes / granularity * granularity);
            var totalBytes = file.Bytes;

            m_Logger.LogDebug("Resumable upload of {file} [{size}] in chunks of {chunk}",
                              file.ShortFilePath, totalBytes.AsHumanReadableBytes("MB"), chunkSize.AsHumanReadableBytes("KB"));

            var buffer = ArrayPool<byte>.Shared.Rent((int)chunkSize);
            try
            {
                using var stream = file.OpenStream();
                long offset = 0;
                var resumes = 0;

                while (true)
                {
                    var length = (int)Math.Min(chunkSize, totalBytes - offset);
                    var isLast = offset + length >= totalBytes;

                    stream.Position = offset;
                    await stream.ReadExactlyAsync(buffer.AsMemory(0, length), cancellationToken);

                    var (uploadToken, failure) = await UploadChunk(sessionUrl, buffer, length, offset, isLast, cancellationToken);
                    if (failure is null)
                    {
                        if (isLast)
                            return uploadToken;

                        offset += length;
                        m_Logger.LogTrace("Uploaded {received} of {total} for {file}",
                                          offset.AsHumanReadableBytes("MB"), totalBytes.AsHumanReadableBytes("MB"), file.ShortFilePath);
                        continue;
                    }

                    if (++resumes > m_MaxResumes)
                        throw new ResumableUploadException($"Upload of {file.ShortFilePath} failed after {m_MaxResumes} resumes: {failure.Description}");

                    var delay = GetResumeDelay(failure.StatusCode, resumes);
                    m_Logger.LogWarning("Chunk upload failed for {file} at offset {offset} (resume {resume} of {total}): {error}. Resuming in {delay:0.0}s.",
                                        file.ShortFilePath, offset, resumes, m_MaxResumes, failure.Description, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);

                    // Google may have stored some (or all) of the failed chunk - continue from what it reports.
                    offset = await QueryReceivedBytes(sessionUrl, file, cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<(string SessionUrl, long Granularity)> StartSession(LocalFile file, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(UploadsPath, "start");
            request.Headers.TryAddWithoutValidation("X-Goog-Upload-Content-Type", file.MimeType);
            request.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
            request.Headers.TryAddWithoutValidation("X-Goog-Upload-Raw-Size", file.Bytes.ToString());
            request.Content = new ByteArrayContent([]);

            using var response = await m_HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccess(response, $"start resumable upload of {file.ShortFilePath}");

            var sessionUrl = GetHeader(response, "X-Goog-Upload-URL")
                             ?? throw new ResumableUploadException($"Start resumable upload of {file.ShortFilePath} returned no X-Goog-Upload-URL.");

            var granularity = long.TryParse(GetHeader(response, "X-Goog-Upload-Chunk-Granularity"), out var g) && g > 0
                                  ? g
                                  : DefaultChunkGranularity;

            return (sessionUrl, granularity);
        }

        private async Task<(string UploadToken, ChunkFailure Failure)> UploadChunk(string sessionUrl, byte[] buffer, int length, long offset, bool isLast, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(sessionUrl, isLast ? "upload, finalize" : "upload");
            request.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", offset.ToString());
            request.Content = new ByteArrayContent(buffer, 0, length);

            try
            {
                using var response = await m_HttpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK)
                    return (null, new ChunkFailure(response.StatusCode, $"{(int)response.StatusCode} {response.StatusCode}"));

                return (isLast ? await response.Content.ReadAsStringAsync(cancellationToken) : null, null);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or TimeoutRejectedException or OperationCanceledException)
            {
                // Interrupted (network error, or per-attempt timeout from the resilience pipeline) - resume via query.
                return (null, new ChunkFailure(null, ex.Message));
            }
        }

        private async Task<long> QueryReceivedBytes(string sessionUrl, LocalFile file, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(sessionUrl, "query");
            request.Content = new ByteArrayContent([]);

            using var response = await m_HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccess(response, $"query resumable upload of {file.ShortFilePath}");

            var status = GetHeader(response, "X-Goog-Upload-Status");
            if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
                throw new ResumableUploadException($"Resumable upload of {file.ShortFilePath} is no longer active (status '{status}').");

            if (!long.TryParse(GetHeader(response, "X-Goog-Upload-Size-Received"), out var received))
                throw new ResumableUploadException($"Query of resumable upload of {file.ShortFilePath} returned no X-Goog-Upload-Size-Received.");

            m_Logger.LogDebug("Google has received {received} of {total} bytes for {file}", received, file.Bytes, file.ShortFilePath);
            return received;
        }

        private static HttpRequestMessage CreateRequest(string url, string command)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            // Scheme only - AuthenticatedHttpClientHandler replaces this with the current bearer token.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer");
            request.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", command);
            return request;
        }

        private static async Task EnsureSuccess(HttpResponseMessage response, string description)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync();
            throw new ResumableUploadException($"Failed to {description}: {(int)response.StatusCode} {response.StatusCode} {body}");
        }

        private static string GetHeader(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

        /// <summary>Exponential backoff with jitter; at least 30s for 429 as Google requires.</summary>
        private static TimeSpan GetResumeDelay(HttpStatusCode? statusCode, int resume)
        {
            var baseDelay = statusCode == HttpStatusCode.TooManyRequests ? s_TooManyRequestsBaseDelay : s_ResumeBaseDelay;
            var delay = baseDelay * Math.Pow(2, resume - 1) + s_ResumeBaseDelay * Random.Shared.NextDouble();
            return delay < s_MaxResumeDelay ? delay : s_MaxResumeDelay;
        }

        private record ChunkFailure(HttpStatusCode? StatusCode, string Description);
    }

    public class ResumableUploadException : Exception
    {
        public ResumableUploadException(string message) : base(message)
        {
        }
    }
}
