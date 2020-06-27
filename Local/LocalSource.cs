using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Local
{
    public class LocalSource
    {
        private readonly DirectoryInfo m_RootDir;

        public List<LocalPhotoAlbum> PhotoAlbums { get; private set; }

        private int? m_TotalPhotos;
        public int TotalFiles => m_TotalPhotos ?? (m_TotalPhotos = PhotoAlbums.Sum(a => a.Files.Count)).Value;

        public LocalSource(IOptions<LocalSettings> localSettings)
        {
            m_RootDir = new DirectoryInfo(localSettings.Value.LocalFolderRoot);
        }

        public void Load()
        {
            PhotoAlbums = m_RootDir.EnumerateDirectories()
                                   .Select(d => new LocalPhotoAlbum(d))
                                   .ToList();
        }
    }

    public class LocalPhotoAlbum
    {
        private DirectoryInfo m_Dir;

        public List<LocalFile> Files { get; }

        public LocalPhotoAlbum(DirectoryInfo dir)
        {
            m_Dir = dir;

            Files = m_Dir.EnumerateFiles()
                               .Select(f => new LocalFile(f))
                               .ToList();
        }
    }

    public class LocalFile
    {
        private FileInfo m_File;

        public LocalFile(FileInfo file)
        {
            m_File = file;
        }
    }
}