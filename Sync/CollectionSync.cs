using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GooglePhotoSync.Sync
{
    public class CollectionSync
    {
        private readonly Func<AlbumSync> m_SyncFactory;
        private readonly SyncStateFile m_SyncStateFile;
        private readonly ILogger<CollectionSync> m_Logger;

        public CollectionSync(Func<AlbumSync> syncFactory, SyncStateFile syncStateFile, ILogger<CollectionSync> logger)
        {
            m_SyncFactory = syncFactory;
            m_SyncStateFile = syncStateFile;
            m_Logger = logger;
        }

        public async Task SyncCollection(CollectionDiff diff)
        {
            //var syncState = await m_SyncStateFile.Load();
            
            foreach (var partial in diff.PartialSyncedAlbums)
            {
                /*
                // Even if Google has different number, skip if local record says we've synced the number of files locally already (at the moment, avoid re-syncing just because a new file appears in google etc.)
                // We could instead just sync only if less files in google than local.
                // But for now, don't sync unless local has changed (assuming what is google hasn't changed to!)
                if (syncState.GetFolderState(partial.Local.Name) == partial.Local.TotalFiles)
                {
                    m_Logger.LogInformation($"Skipping partial: '{partial.Local.Name}' (Local: {partial.Local.TotalFiles})");
                    continue;
                }
                */

                m_Logger.LogInformation($"Syncing partial: '{partial.Local.Name}' (Local: {partial.UnsyncedPhotos.Count} [{partial.UnsyncedPhotoTotalBytes.AsHumanReadableBytes("MB")}], Google: {partial.Google.MediaItemsCount})");
                var sync = m_SyncFactory();
                var filesSynced = await sync.SyncPartialAlbum(partial);
                //syncState.SetFolderState(partial.Local.Name, filesSynced);
                //await m_SyncStateFile.Save(syncState);
            }

            foreach (var local in diff.NeverSyncedAlbums)
            {
                m_Logger.LogInformation($"Syncing never synced: '{local.Name}' {local.TotalFiles} [{local.TotalBytes.AsHumanReadableBytes("MB")}]");
                var sync = m_SyncFactory();
                var filesSynced = await sync.SyncAlbum(local, null);
                //syncState.SetFolderState(local.Name, filesSynced);
                //await m_SyncStateFile.Save(syncState);
            }
        }
    }
}