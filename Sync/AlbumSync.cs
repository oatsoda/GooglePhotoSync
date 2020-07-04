using System.Collections.Generic;
using System.Linq;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GooglePhotoSync.Sync
{
    public class AlbumSync
    {
        private readonly IGooglePhotosApi m_GooglePhotosApi;
        private readonly ILogger<AlbumSync> m_Logger;

        public AlbumSync(IGooglePhotosApi googlePhotosApi, ILogger<AlbumSync> logger)
        {
            m_GooglePhotosApi = googlePhotosApi;
            m_Logger = logger;
        }

        public async Task SyncAlbum(LocalPhotoAlbum local, GoogleAlbum album)
        {
            album ??= await CreateAlbum(local.Name);

            await SyncFiles(local, album);
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

        private async Task SyncFiles(LocalPhotoAlbum localAlbum, GoogleAlbum googleAlbum)
        {
            // Not checking if file exists at the moment because I think we have to
            // use the MediaItem Search with AlbumId to get all the photos and inspect the
            // filename. (also filenames could be duplicated but unlikely in a local album folder)

            const int batchSize = 2; //20; // limit is 50

            var uploaded = new List<UploadedFile>(batchSize);

            var x = 0;
            foreach (var file in localAlbum.Files)
            {
                m_Logger.LogDebug($"Uploading [{file.Bytes.AsHumanReadableBytes("KB")}KB] {file.FilePath}");
                var uploadToken = await m_GooglePhotosApi.UploadFile(file.OpenStream(), file.MimeType);
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

                if (++x == batchSize)
                {
                    await CreateMediateItemsBatch(localAlbum, googleAlbum, uploaded);
                    x = 0;
                    uploaded.Clear();
                }
            }

            if (uploaded.Any())
                await CreateMediateItemsBatch(localAlbum, googleAlbum, uploaded);
        }

        private async Task CreateMediateItemsBatch(LocalPhotoAlbum localAlbum, GoogleAlbum googleAlbum, List<UploadedFile> uploaded)
        {
            m_Logger.LogDebug($"Creating {uploaded.Count} media items in {localAlbum.Name} [{googleAlbum.Id}]");
            var response = await m_GooglePhotosApi.BatchCreateMediaItems(new BatchCreateMediaItemsRequest
                                                                         {
                                                                             albumId = googleAlbum.Id,
                                                                             newMediaItems = uploaded.Select(u => u.CreateMediaItem).ToList()
                                                                         }
                                                                        );

            var failures = response.newMediaItemResults.Where(r => r.status.message != "Success").ToList();

            if (failures.Any())
            {
                // TODO: Retry, handling?
                foreach (var failure in failures)
                {
                    var upload = uploaded.Single(u => u.CreateMediaItem.simpleMediaItem.uploadToken == failure.uploadToken);
                    m_Logger.LogWarning($"Failure uploading {upload.LocalFile.FileName}: {failure.status.message} [{failure.status.code}]");
                }
            }
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