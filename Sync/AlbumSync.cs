using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GooglePhotoSync.Sync
{
    public class AlbumSync
    {
        private readonly IGooglePhotosApi m_GooglePhotosApi;
        private readonly GooglePhotosUploader m_Uploader;
        private readonly SyncSettings m_Settings;
        private readonly ILogger<AlbumSync> m_Logger;

        public AlbumSync(IGooglePhotosApi googlePhotosApi, GooglePhotosUploader uploader, IOptions<SyncSettings> settings, ILogger<AlbumSync> logger)
        {
            m_GooglePhotosApi = googlePhotosApi;
            m_Uploader = uploader;
            m_Settings = settings.Value;
            m_Logger = logger;
        }

        public async Task<int> SyncAlbum(LocalPhotoAlbum local, GoogleAlbum album)
        {
            album ??= await CreateAlbum(local.Name);

            return await SyncFiles(local.Name, local.Files, album);
        }

        public async Task<int> SyncPartialAlbum(CollectionDiff.PartialSyncedAlbum partial)
        {
            return await SyncFiles(partial.Local.Name, partial.UnsyncedPhotos, partial.Google);
        }

        private async Task<GoogleAlbum> CreateAlbum(string albumName)
        {
            m_Logger.LogDebug($"Creating album '{albumName}'");
            return await m_GooglePhotosApi.CreateAlbum(new PostAlbumRequest
            {
                album = new PostAlbumRequest.NewAlbum
                {
                    title = albumName
                }
            });
        }

        private async Task<int> SyncFiles(string localAlbumName, List<LocalFile> localAlbumFiles, GoogleAlbum googleAlbum)
        {
            var uploaded = new ConcurrentBag<UploadedFile>();

            var totalSuccessfulUploads = 0;
            var sw = new Stopwatch();

            foreach (var batch in localAlbumFiles.OrderBy(f => f.FileName).Chunk(m_Settings.BatchSize))
            {
                uploaded.Clear();

                var batchBytes = batch.Sum(f => f.Bytes);
                sw.Restart();

                await Parallel.ForEachAsync(
                    batch.OrderBy(f => f.FileName),
                    new ParallelOptions { MaxDegreeOfParallelism = m_Settings.ParallelUploads },
                    async (file, cancellationToken) =>
                    {
                        m_Logger.LogDebug($"Uploading [{file.Bytes.AsHumanReadableBytes("KB")}] {file.ShortFilePath}");

                        string uploadToken;
                        try
                        {
                            // Raw or resumable depending on size; transient failures are retried / resumed inside.
                            uploadToken = await m_Uploader.Upload(file, cancellationToken);
                        }
                        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Retries / resumes exhausted (or a non-transient error). Don't fail the whole batch - the other files
                            // can still be added. This one will be picked up on the next sync.
                            m_Logger.LogError("Failed to upload {file}, skipping: {error}", file.ShortFilePath, ex.Message);
                            return;
                        }

                        uploaded.Add(new UploadedFile(file, new BatchCreateMediaItemRequest
                        {
                            description = "",
                            simpleMediaItem = new BatchCreateMediaItemRequest.MediaItem
                            {
                                uploadToken = uploadToken,
                                fileName = file.FileName
                            }
                        }
                                                     ));
                        m_Logger.LogDebug($"Uploaded {file.ShortFilePath}");
                    });

                sw.Stop();
                var bytesPerMS = batchBytes / sw.Elapsed.TotalMilliseconds;
                m_Logger.LogDebug($"Uploaded {batchBytes}B in {sw.Elapsed.TotalMilliseconds} which is {bytesPerMS} B/Ms. Batch: {m_Settings.BatchSize}, Parallel: {m_Settings.ParallelUploads}.");

                if (uploaded.IsEmpty)
                    continue;

                totalSuccessfulUploads += await CreateMediateItemsBatch(localAlbumName, googleAlbum, uploaded);
                m_Logger.LogInformation($"{totalSuccessfulUploads} of {localAlbumFiles.Count} uploaded.");
            }

            return totalSuccessfulUploads;
        }

        private async Task<int> CreateMediateItemsBatch(string localAlbumName, GoogleAlbum googleAlbum, IReadOnlyCollection<UploadedFile> uploaded)
        {
            m_Logger.LogDebug($"Creating {uploaded.Count} media items in {localAlbumName} [{googleAlbum.Id}]");
            var response = await m_GooglePhotosApi.BatchCreateMediaItems(new BatchCreateMediaItemsRequest
            {
                albumId = googleAlbum.Id,
                newMediaItems = uploaded.Select(u => u.CreateMediaItem).ToList()
            });

            var failures = response.newMediaItemResults.Where(r => r.status.message != "Success" && r.status.message != "OK").ToList();
            var success = response.newMediaItemResults.Count - failures.Count;

            if (failures.Count > 0)
            {
                // TODO: Retry, handling?
                foreach (var failure in failures)
                {
                    var upload = uploaded.Single(u => u.CreateMediaItem.simpleMediaItem.uploadToken == failure.uploadToken);
                    m_Logger.LogWarning($"Failure uploading {upload.LocalFile.FileName}: {failure.status.message} [{failure.status.code}]");
                }
            }

            m_Logger.LogDebug($"Successfully uploaded {success} media items in {localAlbumName} [{googleAlbum.Id}]");
            return success;
        }

        private class UploadedFile
        {
            public LocalFile LocalFile { get; }
            public BatchCreateMediaItemRequest CreateMediaItem { get; }

            public UploadedFile(LocalFile localFile, BatchCreateMediaItemRequest createMediaItem)
            {
                LocalFile = localFile;
                CreateMediaItem = createMediaItem;
            }

        }
    }
}