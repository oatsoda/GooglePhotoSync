# GooglePhotoSync
Client to Sync local Folderised photo Albums to Google Photos

# Expected folder layout:

```
Album Name 1/
Album Name 1/photo1_011211_090000.jpg
Album Name 1/photo2_011211_090100.jpg
Album Name 2/
Album Name 2/photo1_021211_130100.jpg
Album Name 2/photo2_021211_130100.jpg
```

***Note that Google does some dupe checking on photos that you upload, so two same named files can mean that the latter gets ignore. Ideally have uniquely named files (most photos have timestamp in filename)***

# Local development
Copy `appsettings.Example.json` to `appsettings.Development.json` and plug in your settings.

# TODO

- Support larger files by resumable upload? https://developers.google.com/photos/library/guides/resumable-uploads
    - "The suggested file size for images is less than 50 MB. Files above 50 MB are prone to performance issues."

# Authentication

- Client ID and Secret from App created in Google console https://console.cloud.google.com/apis/api/
- Add "Photos Library API"
- App startup will ask you to login and give permission.

# Diff/Sync Logic

1. Retrieve all Albums from Google (which includes count of items in each)
2. Retrieve all Folders and Files from local store.
3. Match Albums by name
   - If not exists at Google, upload all files.
   - If exists and _same_ number of files, skip.
   - If exists and _different_ number of files, then
     - Retrieve list of files for the Album at Google
     - If any missing (by FileName) upload those.
