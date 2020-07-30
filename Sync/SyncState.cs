using System;
using System.Collections.Generic;

namespace GooglePhotoSync.Sync
{
    public class SyncState
    {
        public Dictionary<string, FolderSyncState> Folders { get; set; }

        public class FolderSyncState
        {
            public string Name { get; set; }
            public DateTimeOffset SyncDate { get; set; }
            public int FilesSynced { get; set; }

            // ReSharper disable once UnusedMember.Local - serialisation
            private FolderSyncState() { }

            public FolderSyncState(string name, DateTimeOffset syncDate, int filesSynced)
            {
                Name = name;
                SyncDate = syncDate;
                FilesSynced = filesSynced;
            }
        }

        public SyncState()
        {
            Folders = new Dictionary<string, FolderSyncState>();
        }

        public int GetFolderState(string name)
        {
            return !Folders.ContainsKey(name) 
                       ? 0 
                       : Folders[name].FilesSynced;
        }

        public void SetFolderState(string name, int filesSynced)
        {
            Folders[name] = new FolderSyncState(name, DateTimeOffset.Now, filesSynced);
        }
    }
}