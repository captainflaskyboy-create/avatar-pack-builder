# Avatar Pack Builder (APB)

Create avatar packs from sprite sheets, assign animation roles, preview playback,
and export PNG frames, app icons, animated GIFs, or animation sprite sheets.
Made by **Captain Flasky Studio**.

APB is a standalone Avalonia desktop application. You do not need Stream Avatar
Studio or PixelSpriteTalker to build or run it.

## Download for Windows

Download `AvatarPackBuilder-v1.0.0-win-x64.zip` from
[GitHub Releases](https://github.com/captainflaskyboy-create/avatar-pack-builder/releases),
extract the entire ZIP, and run `AvatarPackBuilder.exe`.
The Windows x64 release includes .NET; no separate runtime installation is needed.
The application is unsigned. License files accompany the executable.

Verify the download using the release's `SHA256SUMS.txt` and PowerShell:

```powershell
Get-FileHash ./AvatarPackBuilder-v1.0.0-win-x64.zip -Algorithm SHA256
```

## Make an avatar pack

1. Open **1 Import**, choose **Import Sheet...**, and select your sprite sheet.
2. Set rows, columns, cell size, offsets, and spacing so the green grid matches
   your sheet. Select **Use These Slice Settings**, or use **2 Slice** and
   **Apply Slice Grid**.
3. Select a sliced frame. In **Assign**, choose a mood and assign the required
   filename roles. A frame can have multiple roles. Add slots or a custom mood
   when needed.
4. Preview the assigned poses and mouth frames using the playback controls.
5. Optionally use **3 App Icon**, select a frame, choose **Use Selected Frame as Icon**,
   and select icon sizes.
6. In **4 Export**, choose an export folder and pack name, then **Export Pack**.
   Review any missing-frame warning before importing the result into another app.

Pack names use letters, numbers, hyphens, or underscores. Exported role filenames
include `normal_idle001.png` and `normal_talk001.png`, the two minimum required
frames for Stream Avatar Studio. Additional roles include blink, small/large
mouth shapes, and other moods. An optional icon is named `<pack-name>_app_icon.ico`.
Import the resulting pack folder through Stream Avatar Studio's **Import Avatar Pack...**.

## Export an animation

Use **5 Animate**, click sliced frames in playback order, and use the Animate
panel to preview the sequence. A frame may appear more than once to hold a pose.
Set the name and speed, then export a GIF or PNG sprite sheet. These animation
exports are separate from role-based avatar-pack exports.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
From the repository folder:

```powershell
dotnet restore AvatarPackBuilder.csproj --locked-mode
dotnet build AvatarPackBuilder.csproj -c Debug --no-restore
dotnet run --project AvatarPackBuilder.csproj -c Debug --no-build
```

The first restore needs internet access to NuGet. Package versions are pinned
in the project and `packages.lock.json`. Windows x64 is the verified binary
target; macOS and Linux operation has not been verified for this release.

To create the Windows distribution on Windows with PowerShell 7:

```powershell
./scripts/package-windows.ps1
```

The script restores locked dependencies, publishes a self-contained executable,
and creates the ZIP and checksum under `artifacts/`. Build intermediates, local
paths, debug symbols, and personal configuration are excluded from the release.

## License

APB source and its included app icon are licensed under the [MIT License](LICENSE).
Copyright (c) 2026 Captain Flasky Studio. Third-party dependencies retain their
own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and `legal/`.
Images you load and avatar packs you create remain subject to their owners' terms.
No avatar sprite sheets or example character artwork are bundled.

Report bugs and suggest changes through this repository's GitHub Issues.

## Dependency security status

The first release preserves the original image-processing code and retains
ImageSharp 3.1.11, which has known moderate and high severity NuGet advisories.
These dependency issues are unresolved in v1.0.0. Process only images from
sources you trust; the file picker's filters are not a security boundary.

The intended fix is to migrate image processing and exports to SkiaSharp,
following the approach used in Stream Avatar Studio. This migration is planned
and is not included in this release. It must preserve frame naming, transparency,
icon sizes, GIF timing, and avatar-pack compatibility.

ImageSharp advisory references:
- [ICC profile allocation](https://github.com/advisories/GHSA-gwg2-r3hj-4w44)
- [Histogram index validation](https://github.com/advisories/GHSA-j3p4-wp97-rph4)
- [TIFF T4 output buffer](https://github.com/advisories/GHSA-j9gm-c75j-xc9q)
- [TIFF T6 output buffer](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w)
- [BigTIFF decoder loop](https://github.com/advisories/GHSA-wmxv-xphr-5c9g)
