# Tacview Singleplayer Recorder

Records VTOL VR single-player missions as Tacview ACMI 2.2 files for
post-flight review.

## Status

**0.5.2-beta.1 — community beta.** Mission recording now uses native
Unity/VTOL VR state and no longer depends on VTOLAPI initialization. See the
[release notes](docs/RELEASE_NOTES_0.5.2_BETA.md).

Target Practice on VTOL VR v1.13.4m1 produced a finalized 26-minute, 57-second
recording on 2026-11-07 even though VTOLAPI threw during initialization.

A fresh Target Practice flight on VTOL VR v1.12.6m1 was checked on
2026-09-09: 20 minutes 27 seconds, 234 objects, 120 sampled gun bullets,
51 chaff releases and 17 flares. The local terrain cache was installed and
activated, the recording finalized, and the recorder logged no warnings or
errors. The tester reported that playback worked.

Four later Target Practice recordings on v1.13.4m1 from 2026-09-26 were reviewed
on 2026-10-31: all four finalized, have monotonic frame times, and have no matching
temporary file. The log includes unavailable-entity sample warnings. These are
historical 0.5.1 results, not a gameplay test of the new lifecycle.

Akutan terrain alignment/startup performance, the second cannon family,
early exit during terrain export, and multiplayer suppression still need
broader live coverage. These remain beta validation targets, not claims of
completed testing. See [TESTING.md](TESTING.md).

## Recorded data

- Player and AI aircraft
- Missiles, bombs, and rockets
- Ground and sea units
- Airbases, bullseyes, and mission information
- Flares and chaff
- Live `Projectile+Bullet` samples from real VTOL VR Bullet instances for observed gun emissions
- Tacview model hints for recognized VTOL VR aircraft, ground vehicles, and
  the reviewed Carrier/Escort Cruiser sea families
- A local Tacview 1.9.5 custom elevation map generated from VTOL VR's active
  gameplay height data

The model hints are visual approximations, including the ship proxies. Original
VTOL VR names remain unchanged. No Tacview or VTOL VR model files are included.

When the mod loads, it automatically installs or updates its bundled
[aircraft-label definitions](tacview/README.md) for all six flyable aircraft.
This fixes labels such as **F-4** for an EF-24G with the correct F-14 model.
Restart Tacview after the first installation or an update; existing recordings
also receive the corrected labels. No separate XML installation is needed.

## Requirements

- VTOL VR
- VTOL VR Mod Loader
- Tacview 1.9.5 or newer
- .NET 8 SDK, only when building from source

VTOLAPI is optional for this recorder. Other mods may still need it.

## Installation

Subscribe via [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504)
or get the ZIP from [GitHub Releases](https://github.com/kerimala/vtolvr-tacview-recorder/releases).

For a manual install, close VTOL VR and the Mod Loader and extract the folder
`VTOLVRTacviewSPRecorder` into your game's `@Mod Loader/Mods/` directory.
The mod folder contains `VTOLVRTacviewSPRecorder.dll` and `item.json`.
Do not install both a manual copy and a Workshop copy of this recorder.

No game dependencies, terrain caches, or debug symbols are included.

## Usage

1. Start VTOL VR through the Mod Loader.
2. Enter a supported single-player mission.
3. Fly the mission normally.
4. Leave the mission normally so the recording can be finalized.

Recordings are written automatically to:

```text
Documents\Tacview\VTOL VR
```

Open the finished `.acmi` file in Tacview. A remaining `.acmi.tmp` file means
the recording was interrupted or could not be finalized.

## Local terrain companion

The recorder does not put terrain in the ACMI file. After VTOL VR reports
`scenarioReady`, it reads the active generated gameplay heightfield where one
exists. A secondary static path supports authoritative Unity
`Terrain`/`TerrainData` tiles. Akutan has neither of those game objects, so the
candidate instead accepts only its verified `akutan2/Platoons`
`multiRes_terrain.*` non-convex MeshColliders whose readable `akutan2Colored_*`
shared mesh is also the paired MeshFilter/renderer mesh. It snapshots selected
runtime mesh vertices on Unity's main thread, applies the real transforms, then
converts world positions through VTOL global/GPS helpers before interpolating a
pure heightfield index. Where Unity provides an `LODGroup`, it must expose one
selected level; hierarchy nesting alone is not treated as an LOD contract.
Actual overlapping levels, vertical faces, foreign meshes, and ambiguous
surfaces fail closed. On the exact reviewed Akutan lattice only, each uncovered
four-neighbour component must touch an outer raster edge and is then emitted as
0 m MSL ocean; an enclosed uncovered component fails closed. Diagonal contact
alone is not boundary connectivity. It does not decode `fallbackHeightmap` or
use Physics raycasts. Source discovery has a short bounded retry before the
ACMI writer/header is created; an unsupported map records normally without
`MapId`.

Generated/static exports are capped at 1025 x 1025 samples. The bounded mesh
snapshot path is fixed at the independently reviewed 508 x 508 lattice and
resolves every emitted sample before a `MapId` is prepared. Snapshotting,
indexing, and topology resolution currently run synchronously on Unity's main
thread; the resulting startup pause remains an open beta performance test. Export
yields every four rows, and only Core file hashing/writes move to a worker
thread. If terrain export fails, the ACMI recording continues.

Recorder-owned terrain files are installed only in the current user's folder:

```text
%APPDATA%\Tacview\Data\Terrain\Custom\
```

Each terrain version consists of three `VTOLVR.*` files: one signed 16-bit
little-endian `.raw`, one Tacview 1.9.5 `.xml`, and one `.sha256` integrity
sidecar. The recorder never edits `CustomHeightmapList.xml`, never writes to
`Program Files`, and never modifies files without the `VTOLVR.` prefix.

The ACMI header contains a matching value such as:

```text
0,MapId=VTOLVR.e-coastbay.0123456789abcdef
```

The value is provisional while the mission is running. It remains an active
`0,MapId=...` property in the finalized ACMI only after the matching RAW/XML
cache has been verified or installed. If the mission ends early or terrain
startup, sampling, or file I/O fails, finalization changes only that reserved
header prefix to the valid
[ACMI comment](https://raia-software-inc.gitbook.io/tacview/technical-documentation/acmi-telemetry-file-format#comments)
`//MapId=...`. Auto Terrain Switch then cannot select a missing elevation map.
If even that safety rewrite or the final atomic move fails, the recorder retains
the recoverable `.acmi.tmp` instead of publishing an inconsistent final file.

The suffix fingerprints the active terrain, its map settings, coordinates,
source-file metadata, and a 65 x 65 sample grid. Mesh sources additionally
include their verified mesh identities, vertex/triangle metadata, transformed
bounds, and selected hierarchy keys. If a creator changes terrain while reusing
the same VTOL map ID, a new terrain ID is normally generated; old files are
retained so old recordings still resolve to their matching map.

### Tacview setup

1. Use Tacview 1.9.5 or newer.
2. In `Add-ons -> Enable/Disable Add-ons`, enable the bundled **Advanced
   Options** add-on.
3. In `Add-ons -> Advanced Options`, keep **Auto Terrain Switch** enabled. It
   reads the ACMI `MapId` and selects the elevation map whose XML `Id` has the
   exact same case-sensitive value.
4. Complete a fresh VTOL VR mission and wait for the recorder's `Tacview
   terrain installed` or `Tacview terrain cache is current` log line.
5. Start Tacview afterward. If Tacview was already open when a new XML was
   generated, close and restart it before opening the ACMI.
6. Keep Tacview's real terrain display enabled.

The companion supplies actual 3D height relief only. It intentionally does not
copy or generate VTOL VR ground imagery. Tacview therefore continues to use its
own colors/textures unless the user separately configures a lawful texture.
See [LEGAL_AND_PRIVACY.md](LEGAL_AND_PRIVACY.md) for the independent-project,
license, EULA, asset, and privacy boundaries.

## Build from source

The project uses the default Steam installation paths. If VTOL VR or the Mod
Loader is installed elsewhere, override the matching MSBuild properties.

```powershell
dotnet restore
dotnet test tests\VTOLVRTacviewSPRecorder.Core.Tests\VTOLVRTacviewSPRecorder.Core.Tests.csproj -c Release
dotnet test tests\VTOLVRTacviewSPRecorder.Lifecycle.Tests\VTOLVRTacviewSPRecorder.Lifecycle.Tests.csproj -c Release
dotnet build VTOLVRTacviewSPRecorder.csproj -c Release -p:DeployMod=false
dotnet msbuild VTOLVRTacviewSPRecorder.csproj -t:CreateTestPackage -p:Configuration=Release -p:DeployMod=false
```

The package is created under:

```text
artifacts\test-package\VTOLVRTacviewSPRecorder
```

`DeployMod=false` prevents the build from installing the mod automatically.

To prepare the versioned local beta ZIP, private test package, DLL-only Workshop
staging, and checksums after running both test suites:

```powershell
.\tools\PrepareBeta.ps1
```

The outputs are under `artifacts\beta\0.5.2-beta.1\`. The script checks the
manifest/DLL versions and staging contents, and neither installs nor uploads.

For a future Workshop upload, a separate PDB-free staging folder can be built
without changing the local installation:

```powershell
dotnet msbuild VTOLVRTacviewSPRecorder.csproj -t:CreateWorkshopStaging -p:Configuration=Release -p:DeployMod=false
```

It contains only the reviewed Release DLL. The official uploader supplies the
Workshop title, description, thumbnail, visibility, and DLL metadata; do not
upload the private test `item.json` or PDB. See
[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) and
[docs/WORKSHOP_LISTING_DRAFT.md](docs/WORKSHOP_LISTING_DRAFT.md) before any
upload.

## Current limitations

- Multiplayer recording is not supported.
- Aircraft, ground, and ship models are visual proxies, not exact replicas.
- Terrain is a heightfield: no buildings, bridges, caves, overhangs, scenery
  meshes, or VTOL VR surface textures are exported.
- Elevation is quantized to whole meters and capped at 1025 x 1025 samples;
  water is represented at sea level rather than as detailed seabed geometry.
- A map change that preserves all sampled heights and source metadata could
  theoretically retain the same fingerprint, although the 65 x 65 grid plus
  file metadata makes that unlikely for a meaningful terrain edit.
- Gun bullets are sampled from the real VTOL VR `Bullet` pool (at most 10 per
  emitter/s, 60 globally, 32 active), not a record of every physical round.
  They follow the actual Bullet transform and end at the real
  `KillBullet`/pool boundary; the recorder never synthesizes Fired, Hit,
  Destroyed, or Timeout events.
- The static Unity-Terrain and Akutan MeshCollider Campaign paths are code- and
  unit-test-covered but not yet live-validated. Quick Flight terrain visual
  presence does not prove Campaign alignment, LOD selection, or Auto Terrain
  Switch behavior.
- VTOL VR or Mod Loader updates may require compatibility changes.

## Terrain data and removal

The manual mod package contains only the DLL and `item.json`; it contains no
exported RAW/XML terrain, VTOL VR map asset, or Tacview asset. The generated
heightfield is a local interoperability cache derived during the user's own
mission. Do not redistribute the generated `.raw` files or extracted/derived
game assets; users must comply with the
[VTOL VR EULA](https://store.steampowered.com/eula/667970_eula_0) and
[Tacview license](https://www.tacview.net/license/en/) terms.

To remove only this mod's Tacview terrain cache, close Tacview and delete only
files whose names begin with `VTOLVR.` from:

```text
%APPDATA%\Tacview\Data\Terrain\Custom\
```

Do not delete the `Custom` directory or any other user's/custom simulator XML,
RAW, texture, or heightmap files.

Technical notes and test instructions are available in [docs](docs/) and
[TESTING.md](TESTING.md).

The project is open source under the [MIT License](LICENSE) for its original
code and artwork. The owner-approved Candidate D is finalized at
[`assets/workshop/thumbnail-512.png`](assets/workshop/thumbnail-512.png);
only the approved artwork is included in the public repository.
The publication process and draft community materials are in
[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md), [CHANGELOG.md](CHANGELOG.md),
[docs/RELEASE_NOTES_0.5.2_BETA.md](docs/RELEASE_NOTES_0.5.2_BETA.md),
and [docs/WORKSHOP_LISTING_DRAFT.md](docs/WORKSHOP_LISTING_DRAFT.md). Publication uses a clean initial Git history. No third-party endorsement is claimed.

Community feedback is triaged through the [GitHub issue
templates](https://github.com/kerimala/vtolvr-tacview-recorder/issues/new/choose).
Reddit is useful for discovery and announcements, but GitHub Issues remain the
canonical triage channel. There is no automatic upload or placeholder support
email. Read [SUPPORT.md](SUPPORT.md) and [LEGAL_AND_PRIVACY.md](LEGAL_AND_PRIVACY.md)
before attaching logs or recordings; they explain the required evidence and
privacy rules.
