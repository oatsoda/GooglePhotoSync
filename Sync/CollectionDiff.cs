using System;
using System.Collections.Generic;
using System.Linq;
using GooglePhotoSync.Google;
using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;

namespace GooglePhotoSync.Sync
{
    public class CollectionDiff
    {
        public int SameAlbumsCount { get; set; }
        public List<LocalGooglePair> PartialSyncedAlbums { get; } = new List<LocalGooglePair>();
        public List<LocalPhotoAlbum> NeverSyncedAlbums { get; } = new List<LocalPhotoAlbum>();

        private string PartialAlbumsTotalBytes => PartialSyncedAlbums.Sum(m => m.Local.TotalBytes).AsHumanReadableBytes("MB");
        private string NeverAlbumsTotalBytes => NeverSyncedAlbums.Sum(l => l.TotalBytes).AsHumanReadableBytes("MB");

        public CollectionDiff(LocalSource localSource, GoogleSource googleSource)
        {
            foreach (var localAlbum in localSource.PhotoAlbums)
            {
                var match = googleSource.Albums.SingleOrDefault(g => string.Equals(g.Title, localAlbum.Name, StringComparison.InvariantCultureIgnoreCase));

                if (match == null)
                    NeverSyncedAlbums.Add(localAlbum);
                else if (int.Parse(match.MediaItemsCount) != localAlbum.TotalFiles)
                    PartialSyncedAlbums.Add(new LocalGooglePair(localAlbum, match));
                else
                    SameAlbumsCount++;
            }
        }

        public override string ToString()
        {
            return $"\tSynced:  {SameAlbumsCount}{Environment.NewLine}\tPartial: {PartialSyncedAlbums.Count} ({PartialAlbumsTotalBytes}){Environment.NewLine}\tNever:   {NeverSyncedAlbums.Count} ({NeverAlbumsTotalBytes})";
        }

        public class LocalGooglePair
        {
            public LocalPhotoAlbum Local { get; }
            public GoogleAlbum Google { get; }

            public LocalGooglePair(LocalPhotoAlbum local, GoogleAlbum google)
            {
                Local = local;
                Google = google;
            }
        }
    }
}