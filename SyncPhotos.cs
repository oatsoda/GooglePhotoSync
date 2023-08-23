using GooglePhotoSync.Google;
using GooglePhotoSync.Local;
using GooglePhotoSync.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace GooglePhotoSync
{
    public class SyncPhotos
    {
        private readonly GoogleBearerTokenRetriever m_GoogleBearerTokenRetriever;
        private readonly LocalSource m_LocalSource;
        private readonly GoogleSource m_GoogleSource;
        private readonly CollectionComparer m_CollectionComparer;
        private readonly CollectionSync m_CollectionSync;
        private readonly ILogger<SyncPhotos> m_Logger;

        public SyncPhotos(GoogleBearerTokenRetriever googleBearerTokenRetriever, 
                          LocalSource localSource, 
                          GoogleSource googleSource,
                          CollectionComparer collectionComparer,
                          CollectionSync collectionSync,
                          ILogger<SyncPhotos> logger)
        {
            m_GoogleBearerTokenRetriever = googleBearerTokenRetriever;
            m_LocalSource = localSource;
            m_GoogleSource = googleSource;
            m_CollectionComparer = collectionComparer;
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
            var collectionDiff = await m_CollectionComparer.Compare(m_LocalSource, m_GoogleSource);
            m_Logger.LogInformation(collectionDiff.ToString());
            m_Logger.LogInformation("Syncing");
            await m_CollectionSync.SyncCollection(collectionDiff);

            m_Logger.LogInformation("Finished. Press Enter to Quit.");                        
            Console.ReadLine();
        }
    }
}