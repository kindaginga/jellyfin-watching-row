# Watching Row

A Jellyfin plugin that puts **Continue Watching** and **Next Up** into one row on the home screen, the way streaming services show a single "Continue Watching" row.

- Every partly watched movie and episode appears.
- Each series you are watching appears once: its partly watched episode if there is one, otherwise its next episode.
- The row is ordered by what you watched most recently.
- The separate Next Up row on the home screen is hidden, since its shows are already in the combined row. A setting brings it back.

It works on the server, so every Jellyfin app that shows Continue Watching gets the combined row without any change on the device. It has been used with Jellyfin Web and the Samsung (Tizen) app.

## Compatibility

Jellyfin **12.4.0**. Built against the `Jellyfin.Controller` and `Jellyfin.Model` 12.4.0 packages for .NET 10.

## Install

1. In the Jellyfin Dashboard, open **Plugins**, then **Repositories**, and add this repository URL:
   `https://raw.githubusercontent.com/kindaginga/jellyfin-watching-row/main/manifest.json`
2. In the plugin **Catalog**, install **Watching Row**.
3. Restart Jellyfin.

To keep the separate Next Up row, open **Plugins**, then **Watching Row**, and check **Keep the separate Next Up row on the home screen**.

## How it works

The plugin adds a step in front of two of Jellyfin's own requests:

- `GET /UserItems/Resume` for videos (the Continue Watching row) is answered with partly watched items plus Jellyfin's Next Up list (with resumable episodes included), one entry per series, newest activity first. Requests for a single library, for music or books, or for a later page are left to Jellyfin.
- `GET /Shows/NextUp` as sent by the home screen (a date cutoff, with no series, library or sort order) is answered with an empty list, which the home screen hides. Series pages, the Shows library and the full Next Up page are left to Jellyfin.

If building the combined row fails, Jellyfin's own row is returned instead. Each combined row is logged with the app that asked for it.

## Build

```
dotnet test
dotnet publish src -c Release -o dist
```

Copy `dist/Jellyfin.Plugin.WatchingRow.dll` into a folder named `Watching Row_<version>` in Jellyfin's `plugins` folder, then restart Jellyfin.

## License

GPL-3.0, the license of the Jellyfin packages it builds on. See `LICENSE`.
