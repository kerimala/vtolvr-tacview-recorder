# Steam Workshop listing

Published publicly on 2026-09-09: [Tacview Singleplayer Recorder](https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504).
The owner-provided Tacview screenshot is uploaded and VTOLAPI is a required item.
Workshop download hash and Mod Manager discovery were verified; a fresh Workshop
in-game smoke test remains an open beta validation target.

Updated to **0.5.1-beta.1** on 2026-09-13. Public description/change note and
subscribed Workshop DLL hash verified against the GitHub release.

**0.5.2-beta.1 update, 2026-11-08.** Retain the thumbnail, visibility, and
Client Side tag. The recorder's required items should be empty.

## Uploader fields

| Field | Release value |
| --- | --- |
| Title | `Tacview Singleplayer Recorder` |
| Upload folder | `artifacts\beta\0.5.2-beta.1\workshop-staging\VTOLVRTacviewSPRecorder` |
| Contents | Exactly `VTOLVRTacviewSPRecorder.dll`; no PDB, private `item.json`, game assembly, Tacview model, generated terrain, or recording. |
| DLL name | `VTOLVRTacviewSPRecorder.dll` |
| Allow Load On Start | `true` |
| Show On Main Menu | `true` |
| Tag | `Client Side` only. Do **not** select `Multiplayer Compatible` or `Host Enforced`. |
| Dependency | None for 0.5.2. |
| Thumbnail | **Owner-approved Candidate D.** Use `assets/workshop/thumbnail-512.png` (512 x 512 PNG, SHA-256 `9A09197C535BFD4E916F608181D2DB7178B395E2F4868A738940B946EB4152E2`). It is original and contains no Mod Loader, VTOL VR, Tacview, or Boundless Dynamics logo. |
| First visibility | `Hidden` (the uploader maps this to Steam private). |
| Beta visibility | `Public` community beta. |
| Public visibility | Published; anonymous page access verified. |

## Workshop description

```text
[h1]Tacview Singleplayer Recorder — 0.5.2-beta.1 Community Beta[/h1]

Records supported VTOL VR single-player missions as Tacview ACMI 2.2 files for post-flight review.

[h2]Features[/h2]
[list]
[*]Aircraft, weapons, ground/sea units, mission context, flares, and chaff
[*]Local Tacview terrain companion with matching MapId switching
[*]Experimental Akutan Campaign terrain path; broader live validation is still requested
[*]Reviewed aircraft and stock Carrier/Escort Cruiser visual proxy shapes
[*]Automatic Tacview labels for all six flyable aircraft, including the EF-24G/F-4 label fix
[*]Live `Projectile+Bullet` samples from real VTOL VR Bullet instances for observed cannon fire
[/list]

[h2]Requirements[/h2]
[list]
[*]VTOL VR Mod Loader
[*]Tacview 1.9.5 or newer
[/list]

[h2]Important[/h2]
[list]
[*]Single-player only. Multiplayer sessions are intentionally not recorded.
[*]0.5.2 detects missions natively and no longer requires VTOLAPI. Other mods may still need it.
[*]Terrain is a locally generated heightfield, not VTOL VR textures or game assets.
[*]Aircraft and ship shapes are visual proxies.
[*]After updating, load the mod once and restart Tacview to activate aircraft labels. Existing recordings also benefit.
[*]0.5.2 finalized a Target Practice recording on VTOL VR 1.13.4m1 despite a VTOLAPI startup error.
[*]Gun bullets follow the real pooled Bullet transform until its lifecycle teardown; they are not an exact round count, hit log, or ballistic replay.
[*]After a newly generated terrain cache, restart Tacview before opening the finalized ACMI and keep Auto Terrain Switch enabled.
[/list]

[h2]Beta feedback[/h2]
Please use the [url=https://github.com/kerimala/vtolvr-tacview-recorder/issues/new?template=bug_report.yml]bug report form[/url] or [url=https://github.com/kerimala/vtolvr-tacview-recorder/issues/new?template=feature_request.yml]feature request form[/url]. Include the recorder, VTOL VR, Mod Loader, and Tacview versions; mission/map; reproduction or use case; and sanitized Player.log, finalized ACMI, and Tacview screenshot evidence where applicable. Read the [url=https://github.com/kerimala/vtolvr-tacview-recorder/blob/main/SUPPORT.md]support and privacy guide[/url] first. Do not attach generated terrain RAW/XML files, extracted game assets, keys, credentials, or recordings containing other players' data.
```

## Future updates

Update the existing Workshop item `3798566504` rather than creating a duplicate.
Stage only the reviewed Release DLL, compare the downloaded hash after uploading,
and check the remaining runtime targets in `RELEASE_CHECKLIST.md`.

See [LEGAL_AND_PRIVACY.md](../LEGAL_AND_PRIVACY.md) for the independent-project,
no-endorsement, asset, EULA, and privacy boundaries.
