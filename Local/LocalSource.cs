using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;

namespace GooglePhotoSync.Local
{
    public class LocalSource
    {
        private readonly LocalSettings m_LocalSettings;
        private readonly DirectoryInfo m_RootDir;

        public List<LocalPhotoAlbum> PhotoAlbums { get; private set; }

        private int? m_TotalFiles;
        public int TotalFiles => m_TotalFiles ?? (m_TotalFiles = PhotoAlbums.Sum(a => a.TotalFiles)).Value;
        
        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = PhotoAlbums.Sum(a => a.TotalBytes)).Value;

        public LocalSource(IOptions<LocalSettings> localSettings)
        {
            m_LocalSettings = localSettings.Value;
            m_RootDir = new DirectoryInfo(localSettings.Value.LocalFolderRoot);
        }

        public void Load()
        {
            PhotoAlbums = m_RootDir.EnumerateDirectories()
                                   .Where(IsNotIgnored)
                                   .Select(d => new LocalPhotoAlbum(d))
                                   .ToList();
        }

        private bool IsNotIgnored(DirectoryInfo dir)
        {
            var name = dir.Name;
            return !m_LocalSettings.IgnoreFolderStartingWith.Any(s => name.StartsWith(s));
        }
    }

    public class LocalPhotoAlbum
    {
        private readonly DirectoryInfo m_Dir;

        public string Name => m_Dir.Name;
        public List<LocalFile> Files { get; }
        
        public int TotalFiles => Files.Count;

        private long? m_TotalBytes;
        public long TotalBytes => m_TotalBytes ?? (m_TotalBytes = Files.Sum(f => f.Bytes)).Value;

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
        private readonly FileInfo m_File;

        public long Bytes => m_File.Length;

        public LocalFile(FileInfo file)
        {
            m_File = file;
        }
    }
}