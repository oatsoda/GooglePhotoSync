﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GooglePhotoSync.Sync
{
    public class CollectionSync
    {
        private readonly Func<AlbumSync> m_SyncFactory;
        private readonly ILogger<CollectionSync> m_Logger;

        public CollectionSync(Func<AlbumSync> syncFactory, ILogger<CollectionSync> logger)
        {
            m_SyncFactory = syncFactory;
            m_Logger = logger;
        }

        public async Task SyncCollection(CollectionDiff diff)
        {
            foreach (var partial in diff.PartialSyncedAlbums)
            {
                m_Logger.LogInformation($"Syncing partial: '{partial.Local.Name}' (Local: {partial.UnsyncedPhotos.Count} [{partial.UnsyncedPhotoTotalBytes.AsHumanReadableBytes("MB")}], Google: {partial.Google.MediaItemsCount})");
                var sync = m_SyncFactory();
                await sync.SyncPartialAlbum(partial);
            }

            foreach (var local in diff.NeverSyncedAlbums)
            {
                m_Logger.LogInformation($"Syncing never synced: '{local.Name}' {local.TotalFiles} [{local.TotalBytes.AsHumanReadableBytes("MB")}]");
                var sync = m_SyncFactory();
                await sync.SyncAlbum(local, null);
            }
        }
    }
}