using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GooglePhotoSync.Sync;

public class CollectionComparer
{
    private readonly IGooglePhotosApi m_GooglePhotosApi;
    private readonly ILogger<CollectionComparer> m_Logger;

    public CollectionComparer(IGooglePhotosApi googlePhotosApi, ILogger<CollectionComparer> logger)
    {
        m_GooglePhotosApi = googlePhotosApi;
        m_Logger = logger;
    }

    public async Task<CollectionDiff> Compare(LocalSource localSource, GoogleSource googleSource)
    {
        var collectionDiff = new CollectionDiff();

        foreach (var localAlbum in localSource.PhotoAlbums)
        {
            var match = googleSource.Albums.SingleOrDefault(g => string.Equals(g.Title, localAlbum.Name, StringComparison.InvariantCultureIgnoreCase));

            if (match == null)
            {
                collectionDiff.NeverSyncedAlbums.Add(localAlbum);
            }
            else 
            { 
                var googleItemsCount = match.MediaItemsCount == null ? 0 : int.Parse(match.MediaItemsCount);
                if (googleItemsCount != localAlbum.TotalFiles)
                {
                    var photos = await LoadGoogleAlbumPhotos(match.Id, localAlbum.Name, googleItemsCount);
                    var partial = new CollectionDiff.PartialSyncedAlbum(localAlbum, match, photos);

                    foreach (var extra in partial.ExtraPhotos)
                        m_Logger.LogInformation("Extra Google File found: {name} in {album} [{mimeType}]", extra.Filename, localAlbum.Name, extra.MimeType);

                    if (partial.UnsyncedPhotos.Count > 0)
                        collectionDiff.PartialSyncedAlbums.Add(partial);
                    else
                        collectionDiff.SameAlbumsCount++;
                }
                else
                {
                    collectionDiff.SameAlbumsCount++;
                }
            }
        }

        return collectionDiff;
    }
        
    private async Task<List<MediaItem>> LoadGoogleAlbumPhotos(string albumId, string localAlbumName, int expected)
    {
        var mediaItems = new List<MediaItem>(expected);

        m_Logger.LogDebug("Count mismatch. Loading {expected} album photos list for {albumName}", expected, localAlbumName);

        SearchMediaItemsResponse response;
        string pageToken = null;
        do
        {
            var request = new SearchMediaItemsRequest { albumId = albumId, pageSize = 100, pageToken = pageToken };
            response = await m_GooglePhotosApi.SearchMediaItems(request);
            if (response.MediaItems != null)
                mediaItems.AddRange(response.MediaItems);
            pageToken = response.NextPageToken;
        } while (response.NextPageToken != null);

        return mediaItems;
    }
}