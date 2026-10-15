# Community beta validation

## 0.5.1-beta.1 aircraft-label test

The DLL embeds the six flyable-aircraft definitions and installs them from
`Main.Awake`. Installer tests cover first install, unchanged-file detection,
replacement, preservation of unrelated files, failure cleanup, and catalog parity.
The definitions themselves were verified in Tacview 1.9.5.201 against both
original EF-24G reporter recordings; the short and long labels are correct.

Local candidate checks before release preparation on 2026-09-13: 201/201 Release tests passed; the net472
Release build had no warnings or errors. The built DLL's embedded-resource
installer passed first-install/current/update checks under Windows .NET CLR 4,
and its installed XML matched the source. Private-package, DLL-only Workshop
staging, and local Mod Loader DLL hashes matched. These checks preceded publication.

Fresh gameplay and mod-load integration remain community testing targets.
The maintainer does not own the EF-24G DLC and approved this community beta
without claiming a fresh EF-24G flight. Testers should:

1. Start VTOL VR with this candidate as the only enabled copy of the recorder.
2. Check the game log for `Tacview aircraft labels are current` or
   `Tacview aircraft labels installed/updated` and no new recorder errors.
3. To exercise first installation, with the game closed move the recorder-owned
   `Tacview.VTOLVR.PlayerAircraft.xml` out of the Tacview database directory into
   a backup folder, then load the mod again. Do not move other custom definitions.
4. Fly and finish an EF-24G single-player mission. Restart Tacview, then open
   the resulting ACMI: expect EF-24G/Mischief labels and the F-14 model.
5. Report the mod/Tacview versions, log result, aircraft labels and model;
   sanitize any shared recordings or logs first.

Release checks on 2026-09-13: 201/201 tests passed in both Debug and Release,
with 0 warnings/errors in both builds. GitHub ZIP and subscribed Workshop DLL
were downloaded and verified against the release build; see RELEASE_CHECKLIST.md.

## Verified on 2026-09-09

Target Practice, F/A-26B, VTOL VR v1.12.6m1, candidate 0.5.0-private-test.6:
1227.183 seconds, 234 objects, 120 gun samples, 51 chaff, 17 flares,
8 missiles, 6 bombs, 12 aircraft, 13 ground vehicles and 3 ships.
All non-static objects have one delete; all recorded parents resolve.
Timestamps are monotonic. No matching temporary file remained and the
recorder logged no warnings/errors. Terrain e_coastbay was installed and
its MapId activated. The tester reported working playback.

The 0.5.0-beta.1 release changed metadata only relative to that candidate.
This does not establish every mission, cannon family, or edge case below.
Akutan, early-exit and multiplayer tests remain open coverage targets.

## Prerequisites

Enable this mod. VTOLAPI is not required. Use Tacview 1.9.5 or newer with
Advanced Options and Auto Terrain Switch enabled. Restart Tacview after new
terrain is generated.

## Expected evidence

Tagged runtime lines are in:

```text
%USERPROFILE%\AppData\LocalLow\Boundless Dynamics, LLC\VTOLVR\Player.log
```

Expected categories include `Aircraft`, `Ground`, `Sea`, `Missile`, `Bomb`, `Rocket`, `Flare`, and `Chaff` registration/removal plus one session start and stop. IDs and names are mission-dependent. The first ownship is `100`; every later object ID is session-unique. Reuse of one pooled `CMFlare` must still allocate a new ID. A team swap should not generate a false removal because Actor unregisters have a 250 ms grace. Repeating sample failures or capture-cap messages more often than the documented throttle are defects.

At session startup, expect:

```text
Countermeasure capture readiness: flare=True, advanced-chaff=True (typed event), particle-chaff=True.
```

Terrain source discovery and mesh preflight complete before the ACMI header is
created; the file/cache job then starts independently after recording is
active. First expect one source line. Quick Flight/custom generated maps should
name the `VTMapGenerator gameplay heightfield`; an Akutan Campaign map with an
unambiguous selected terrain surface should name the
`verified Akutan multi-resolution MeshCollider heightfield` rather than
failing because `VTMapGenerator` has no `hmBdt`:

Akutan snapshotting, indexing, and full 508 x 508 topology resolution are
currently synchronous on Unity's main thread. Measure and preserve the startup
pause when evaluating this beta; automated build success is not
runtime performance evidence.

```text
Tacview terrain source prepared: <source> (<width>x<height>, MapId 'VTOLVR.<map>.<fingerprint>').
```

On a first visit to a terrain fingerprint, then expect one completion line
similar to:

```text
Tacview terrain installed: VTOLVR.<map>.<fingerprint> (<width>x<height>) at '<user XML path>'. Restart Tacview before opening this recording.
```

On later visits to the exact same terrain, expect:

```text
Tacview terrain cache is current: VTOLVR.<map>.<fingerprint>
```

One `Tacview terrain companion is unavailable` or `Tacview terrain export
failed` warning is a terrain defect to preserve, but it must not stop or fault
the ACMI recorder. For Akutan, preserve the exact discovery error: an
unreadable mesh, a mixed/nested LOD, an overlap, or a gap must remain
unsupported instead of receiving a guessed `MapId`. The old Akutan message
about unavailable `VTMapGenerator` height data alone is a regression when the
verified MeshCollider source is selected.
If the mission ends before a first-time export completes, expect a warning that
terrain was not ready and a finalized ACMI containing a commented `//MapId=...`
reservation rather than an active `0,MapId=...`.

The first advanced-radar chaff release must then produce exactly one session
line confirming that the typed event reached the recorder:

```text
Countermeasure advanced chaff typed-event capture received its first release.
```

There must be no Harmony attempt or `ILGeneration` exception for
`AdvRdrChaff()`.

## Suggested 5–10 minute mission

Choose a singleplayer mission containing allied/enemy aircraft, at least one Ground or Sea unit, and weapons usable by the player or AI.

1. At the main menu, confirm the mod loads but no recording session starts.
2. Enter the mission and wait for ownship, other aircraft, and available Ground/Sea registrations. On the first visit to this map version, also wait for the terrain-installed log line before leaving the mission.
3. Fly straight, bank both ways, turn roughly 360 degrees, climb, and descend to recheck the established nine-field Aircraft transform.
4. Fire one guided missile, one bomb, and one unguided rocket where the loadout permits. Note approximate times and intended targets. Let AI fire at least one weapon if practical.
5. Fire the normal aircraft gun for several short bursts. In the ACMI, confirm
   visible `Projectile+Bullet` samples have a real `Parent`, `Coalition`, and
   `Color`, follow the actual Bullet position between updates, and end with an
   eventless Delete after the real `KillBullet`/pool teardown. Capture remains
   bounded at 10 accepted samples per emitter per second, 60 globally, and 32
   active. It is not an accounting of every physical round and does not invent
   a hit event. If an aircraft uses `VTOLCannon`, test that weapon as well.
6. Release one flare, then a rapid flare salvo, then enough later flares to exercise a pooled GameObject generation. Each release must have a fresh Tacview ID, a real moving transform, and a final sample before Delete.
7. Release chaff. If both radar modes are practically selectable, test each: advanced chaff must follow its real 16-slot simulation state; non-advanced chaff must start at the real launcher/particle-burst position and end by particle liveness or the safety TTL. No owner-position substitute is acceptable.
8. Observe a real miss/timeout and a detonation if the mission permits. Do not infer a hit solely from an explosion; compare emitted events with actual game evidence. Countermeasure use must not create a guessed “decoy successful” relation or event.
9. Move near or observe a Ground/Sea unit. A stationary unit should keep its trail/state without repeated blank transform lines; a moving unit should update at roughly 2 Hz.
10. If safe, trigger one actor death and one non-death despawn/pooling case. Death must receive exactly one `Destroyed`/delete sequence; generic despawn/pooling must preserve history and receive a delete without a fabricated `LeftArea` event.
11. End the mission while at least one flare or chaff is active. Confirm the final drain preserves Spawn/final/Delete ordering, then confirm a finalized `.acmi` and no matching `.tmp`.

## Raw ACMI checks before Tacview

Runtime files are expected under:

```text
%USERPROFILE%\Documents\Tacview\VTOL VR\
```

Check the text file for:

- `FileVersion=2.2`, `Theater`, `Category=Singleplayer`, one sanitized
  active `0,MapId=VTOLVR.<map>.<16 lowercase hex characters>` only when the
  matching terrain files were confirmed, and plausible briefing/title fields;
- after an intentionally very short mission or a terrain export failure, no
  active `0,MapId` and exactly one `//MapId=VTOLVR.<map>.<fingerprint>` comment;
- no `ReferenceLongitude` or `ReferenceLatitude`;
- transform order `Longitude|Latitude|Altitude|Roll|Pitch|Yaw|U|V|Heading`;
- `Air`, `Weapon+Missile`, `Weapon+Bomb`, `Weapon+Rocket`, `Ground+Vehicle`, and `Sea+Watercraft` tags only where corresponding real objects existed;
- `Misc+Decoy+Flare` and `Misc+Decoy+Chaff`, `Name=Flare`/`Name=Chaff`, and a valid aircraft `Parent` where ownership was resolved;
- each known VTOL VR aircraft keeps its fictional `Name` and receives only the reviewed exact-match `Shape`; Campaign runtime aliases `FA-26B` and `MANTA` must receive the same reviewed Shape as `F/A-26B` and `Manta UCAV`; unknown/modded aircraft receive no invented Shape;
- the six configured Ground families receive only their exact reviewed Shapes; unknown Ground actors and airbases receive no invented Shape;
- stock Carrier and Escort Cruiser groups receive their reviewed Tacview ship proxies (`Watercraft.CVN-68.obj` and `Watercraft.CG-47.obj`) while retaining the VTOL VR Name. A custom/unknown carrier or decorated vessel without a confirmed canonical prefab name must stay generic rather than inheriting a stock ship Shape;
- sampled gun bullets use `Type=Projectile+Bullet`, real `Parent`, coalition and color, then eventless Delete at the real Bullet lifecycle boundary. They must contain no invented `Event=Fired`, `Event=Hit`, `Destroyed`, or `Timeout` marker and must not be interpreted as one object per physical round;
- `Parent=<HEX>` links to the real launcher and `LockedTarget=<HEX>` only when a registered real target was available at launch;
- no invented `Launcher=`, generic `Target=`, `Event=Fired`, or `Event=Hit` fields;
- `Destroyed` or documented `Timeout|SourceId:...` only where explicit game state supports them; generic disappearance must use an eventless delete, not an inferred `LeftArea`;
- each object deleted once and no later reuse of its ID;
- every `Parent`, `LockedTarget`, `Next`, `SourceId`, and `TargetId` resolves to an object created in the same or an earlier ACMI frame;
- no frames containing only redundant `T=||||||||` lines for stationary Ground/Sea objects;
- no countermeasure ID reused after Delete, including when a `CMFlare` GameObject is pooled and reactivated.

## Terrain companion checks before Tacview

The only managed terrain root is:

```text
%APPDATA%\Tacview\Data\Terrain\Custom\
```

For a recording with a completed/verified terrain export:

1. Copy the exact active `0,MapId` value from the finalized ACMI header. A
   commented `//MapId` means this recording deliberately has no selectable
   terrain companion and the remaining matching-file checks do not apply.
2. Confirm files named `<MapId>.raw`, `<MapId>.xml`, and `<MapId>.sha256`
   exist directly in that directory. No file is allowed under Tacview's
   `Program Files` tree.
3. Confirm the XML `CustomHeightmap Id` (lowercase `d`, not uppercase `ID`)
   exactly equals the ACMI `MapId`, its
   `<File>` exactly names `<MapId>.raw`, and it contains `Visible=false`,
   `BigEndian=0`, `Signed=true`, `AltitudeFactor=1`, `AltitudeOffset=0`, and
   `Projection=Quad`.
4. Confirm RAW length equals `Width * Height * 2`, both dimensions are between
   2 and 1025, and the SHA-256 of the RAW equals the lowercase value in the
   `.sha256` sidecar.
5. Confirm the four XML corners are finite WGS-84 coordinates ordered
   BottomLeft, BottomRight, TopRight, TopLeft. Longitude must increase along
   VTOL global +X/east and latitude along VTOL global +Z/north, using the same
   GPS convention as recorded objects.
6. Re-enter the unchanged map: the `MapId` must remain identical, the cache
   must report current, and the three files must not be rewritten.
7. For a controlled copy of a custom map, change meaningful terrain while
   retaining its `mapID`: the fingerprint suffix should change and a new three
   file set should be created without deleting the old one.
8. Place a harmless foreign `CustomHeightmapList.xml` or differently prefixed
   test file in the directory before the run. Its bytes and timestamp must
   remain unchanged. Remove only the harmless test file afterward.
9. End a first visit before the installed log appears: the finalized ACMI must
   contain only `//MapId=...`, not active `0,MapId=...`. A suppression or ACMI
   move failure must leave `.acmi.tmp` and must not publish a final `.acmi`.
10. **Quick Flight regression:** reproduce the previously visually present
    terrain on a fresh recording. Confirm Tacview no longer creates an unnamed
    `Heightmap#` entry, Auto Terrain Switch selects the XML's exact `Id`, and
    an asymmetric ridge remains in the correct map quadrant.
11. **Akutan Campaign regression:** run Island Defense or Darkness on
    `map_akutan`. Confirm the recorder prepares the
    `verified Akutan multi-resolution MeshCollider heightfield`, emits/uses an
    active `MapId` only after mesh preflight and export verification, and does
    not report only the old missing-`hmBdt` message. Compare one coastal
    mountain/ridge and the mountain between the player and the enemy ships
    between VTOL and Tacview. If the selected runtime mesh LOD is ambiguous or
    incomplete or any uncovered four-neighbour component is enclosed, retain
    the warning and ensure the finalized file has no active `MapId`. Confirm
    boundary-connected ocean is 0 m MSL, and record the synchronous pre-header
    startup pause. This path is not yet live-validated by this document.

The raster is explicitly emitted west-to-east by column and north-to-south by
row, in signed little-endian whole meters MSL. The Akutan source derives each
snapshot vertex from the actual runtime Transform and VTOL world/global/GPS
converters; there are no old Akutan constants, guessed altitude factor,
`fallbackHeightmap` decoder, Physics raycast grid, or Floating Origin offset
baked into the export.

## Tacview check

Close and restart Tacview after a newly generated XML. In `Add-ons ->
Enable/Disable Add-ons`, confirm **Advanced Options** is enabled; in `Add-ons ->
Advanced Options`, confirm **Auto Terrain Switch** is checked. Open the
finalized `.acmi`, never a `.tmp`, and verify:

- no format warning and a duration matching the mission;
- Aircraft retain correct direction, bank, altitude, and curved trails;
- missiles/rockets/bombs originate from the correct parent object and follow plausible trajectories;
- reviewed aircraft Shapes display plausible proxy models while the object list retains the fictional VTOL VR names;
- Campaign `FA-26B` and `MANTA` AI aircraft display their same reviewed proxies as `F/A-26B` and `Manta UCAV`; this is an exact alias check, not a fuzzy-name resolver;
- Artillery, MBT2-E, MAD-4 Launcher, MAD-4 Radar, IR APC, and SAM Radar display their documented visual proxies while retaining their VTOL VR names;
- flares and chaff are visible, parented to the correct aircraft when known, and end at plausible times without displacing missile tracks;
- Ground/Sea objects have plausible location, heading, coalition, color, and lifecycle;
- stock Carrier/Escort Cruiser ship proxies are visible but clearly treated as visual approximations; other/custom ships may remain generic blocks;
- gun samples follow real pooled Bullet lifecycles to their teardown endpoint.
  Do not use their count or endpoint to claim an exact fired-round count or a hit;
- Allied/Enemy bullseyes and non-carrier airbases appear once without duplicated carrier objects;
- object trails remain after deletion while active state ends at the correct time;
- the selected elevation-map ID exactly matches the ACMI `MapId`, while a
  recording from another fingerprint switches to its own matching ID;
- a distinctive mountain/ridge is in the same north/east map quadrant as in
  VTOL VR, proving the RAW is neither vertically flipped nor rotated; compare
  at least one asymmetric feature near a corner and one near the center;
- aircraft and ground units that are on/near the surface sit plausibly on the
  relief, coastline and sea level align with their recorded positions, and a
  climb through a valley shows the expected relative ridge heights;
- Tacview shows true 3D height relief but no VTOL VR ground texture, buildings,
  bridges, caves, overhangs, or scenery meshes. Those are intentionally out of
  scope;
- playback remains responsive in the populated segment.

## Failure evidence

Before retrying, preserve:

- complete `Player.log`;
- all matching `.acmi` and `.acmi.tmp` files;
- the three matching `VTOLVR.<map>.<fingerprint>.*` terrain files (do not
  publish or redistribute the RAW); directory listing and hashes are usually
  sufficient for a report;
- the deployed `item.json` and DLL plus DLL SHA-256 (PDB only for local debug builds);
- approximate UTC event times, mission/map, aircraft/loadout, weapon type, intended target, and observed result;
- Tacview screenshots of the object list and affected timeline.

Keep `.tmp` files. A writer/finalization fault intentionally retains them and must not be logged as a successful output.

## Safe removal

With VTOL VR and the Mod Loader closed, remove exactly the one approved local-mod folder shown above. Do not remove the parent `Mods` folder, Workshop content, baseline archive, or recorded ACMI files.

With Tacview closed, terrain can be removed separately by deleting only files
whose names begin with `VTOLVR.` from:

```text
%APPDATA%\Tacview\Data\Terrain\Custom\
```

Never delete the parent `Custom` folder, `CustomHeightmapList.xml`, or other
simulators'/users' XML, RAW, checksum, or texture files. Generated terrain is a
local interoperability cache derived from the user's installed game; it is not
part of the mod package and must not be redistributed with a release.
