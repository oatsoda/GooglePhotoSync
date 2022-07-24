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

- Improve the sync logic by using the search endpoint to find all mediaitems in an album: https://developers.google.com/photos/library/guides/list#listing-album-contents
- Support larger files by resumable upload? https://developers.google.com/photos/library/guides/resumable-uploads

# Authentication

- Client ID and Secret from App created in Google console https://console.cloud.google.com/apis/api/
- Add "Photos Library API"
- App startup will ask you to login and give permission.

# Diff/Sync Logic

The Google API does not have a "get photo" endpoint, so it's not possible to check each file 
individually (aside from possibly using MediaItem search with Album filter to get all photos in an album?) so therefore the app logic is:

1. Retrieve all Albums from Google (which includes count of items in each)
2. Retrieve all Folders and Files from local store.
3. Match Albums by name
   - If not exists at Google, upload all files.
     - Record count of files uploaded for the album in google.sync file.
   - If exists and _same_ number of files, skip.
   - If exists and _different_ number of files, then
     - If google.sync entry says count of files uploaded equals local, then skip (i.e. don't re-upload because Google has extra file)
     - if google.sync entry says count of files uploaded is different to local folder, then re-upload all (i.e. assume change to the local folder - but note will not delete a file at Google, so perhaps we should only re-upload if google has less than local or local has more than last synced in google.sync)
