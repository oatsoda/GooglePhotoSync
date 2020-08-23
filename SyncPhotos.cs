using System.Threading.Tasks;
using GooglePhotoSync.Google;
using GooglePhotoSync.Local;
using GooglePhotoSync.Sync;
using Microsoft.Extensions.Logging;

namespace GooglePhotoSync
{
    public class SyncPhotos
    {
        private readonly IGoogleBearerTokenRetriever m_GoogleBearerTokenRetriever;
        private readonly LocalSource m_LocalSource;
        private readonly GoogleSource m_GoogleSource;
        private readonly CollectionSync m_CollectionSync;
        private readonly ILogger<SyncPhotos> m_Logger;

        public SyncPhotos(IGoogleBearerTokenRetriever googleBearerTokenRetriever, 
                          LocalSource localSource, 
                          GoogleSource googleSource,
                          CollectionSync collectionSync,
                          ILogger<SyncPhotos> logger)
        {
            m_GoogleBearerTokenRetriever = googleBearerTokenRetriever;
            m_LocalSource = localSource;
            m_GoogleSource = googleSource;
            m_CollectionSync = collectionSync;
            m_Logger = logger;
        }

        public async Task Sync()
        {
            if (!await m_GoogleBearerTokenRetriever.Init())
                return;

            m_Logger.LogInformation("Loading Google Albums");
            await m_GoogleSource.Load();
            m_Logger.LogInformation($"Total Albums: {m_GoogleSource.Albums.Count}");

            m_Logger.LogInformation("Loading local collection");
            m_LocalSource.Load();
            m_Logger.LogInformation($"Total Albums: {m_LocalSource.PhotoAlbums.Count}");
            m_Logger.LogInformation($"Total Files: {m_LocalSource.TotalFiles}");
            m_Logger.LogInformation($"Total Size: {m_LocalSource.TotalBytes.AsHumanReadableBytes("MB")}");

            m_Logger.LogInformation("Comparing");
            var collectionDiff = new CollectionDiff(m_LocalSource, m_GoogleSource);
            m_Logger.LogInformation(collectionDiff.ToString());
            m_Logger.LogInformation("Syncing");
            await m_CollectionSync.SyncCollection(collectionDiff);
        }

        /*
        SYNC THOUGHTS

        1. Retrieve all Folders and FileInfos from local store.
        2. Retrieve all Albums from Google (which includes number of items)
        3. Match Albums by name
        4. For matching Albums, compare file counts
        5. If Google album has less files, then Upload
            a. Use the /v1/uploads to upload the content
            b. Use the upload-token from that to create the Media Item
            c. Use the mediaItemId to add the item to the Album

        Google apparently detects duplicate items so the risk of creating dupes is low. 
        It might be easier though to rename all the older files which don't have unique file names.            
        */
    }
}