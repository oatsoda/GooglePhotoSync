using GooglePhotoSync.Google.Api;
using GooglePhotoSync.Local;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GooglePhotoSync.Sync
{
    public class CollectionDiff
    {
        public int SameAlbumsCount { get; set; }
        public List<PartialSyncedAlbum> PartialSyncedAlbums { get; } = new();
        public List<LocalPhotoAlbum> NeverSyncedAlbums { get; } = new();

        private string PartialAlbumsTotalBytes => PartialSyncedAlbums.Sum(m => m.UnsyncedPhotoTotalBytes).AsHumanReadableBytes("MB");
        private string NeverAlbumsTotalBytes => NeverSyncedAlbums.Sum(l => l.TotalBytes).AsHumanReadableBytes("MB");
        
        public override string ToString()
        {
            return $"\tSynced:  {SameAlbumsCount}{Environment.NewLine}\tPartial: {PartialSyncedAlbums.Count} ({PartialAlbumsTotalBytes}){Environment.NewLine}\tNever:   {NeverSyncedAlbums.Count} ({NeverAlbumsTotalBytes})";
        }

        public class PartialSyncedAlbum
        {
            public LocalPhotoAlbum Local { get; }
            public GoogleAlbum Google { get; }

            public List<LocalFile> UnsyncedPhotos { get; }
            public List<MediaItem> ExtraPhotos { get; }

            public long UnsyncedPhotoTotalBytes { get; }
            
            public PartialSyncedAlbum(LocalPhotoAlbum local, GoogleAlbum google, IReadOnlyCollection<MediaItem> googleAlbumPhotos)
            {
                Local = local;
                Google = google;
                UnsyncedPhotos = Local.Files.Where(f => googleAlbumPhotos.All(g => g.Filename != f.FileName)).ToList();
                UnsyncedPhotoTotalBytes = UnsyncedPhotos.Sum(p => p.Bytes);
                ExtraPhotos = googleAlbumPhotos.Where(g => Local.Files.All(f => f.FileName != g.Filename)).ToList();
            }
        }
    }
}