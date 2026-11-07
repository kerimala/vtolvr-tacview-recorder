# Tacview Singleplayer Recorder — 0.5.2-beta.1

**Community beta, 2026-11-08.**

## Recording startup fix

This candidate addresses [issue #1](https://github.com/kerimala/vtolvr-tacview-recorder/issues/1),
where the mod loaded but never started a recording after a clean installation.
The reporter traced the failure to VTOLAPI initialization. The recorder now
detects mission readiness directly from Unity/VTOL VR, and it no longer loads
or calls VTOLAPI. Thanks to **gollth** for the diagnosis and native-monitoring
proposal in [PR #2](https://github.com/kerimala/vtolvr-tacview-recorder/pull/2).

- Native mission-exit and Unity scene-change callbacks finalize recordings.
- A new scenario or map manager starts a separate recording, including instant
  restarts. An exiting scenario cannot reopen during the exit fade.
- Slow loading continues to be monitored after a diagnostic at 30 seconds.
- Transient native-state errors are logged with a throttle and monitoring resumes.
- Session initialization retries at five-second intervals, up to three attempts
  per mission. Writer faults retain their temporary output and wait for a new mission.
- Unsupported terrain still permits recording without a MapId after bounded discovery.
- Ownship discovery uses the native player actor and existing actor registry.

## Requirements and installation

VTOL VR, VTOL VR Mod Loader, and Tacview 1.9.5 or newer are required.
VTOLAPI is optional for the recorder. Keep it enabled if another mod needs it.

Update through [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504)
or use the release ZIP for a manual install. With the game and loader closed, extract the ZIP's
`VTOLVRTacviewSPRecorder` folder into the game's `@Mod Loader/Mods/` directory
and enable that copy. Disable the subscribed recorder copy first. Test with only
one recorder enabled. To roll back, remove the candidate copy and re-enable the
published 0.5.1-beta.1 recorder and its VTOLAPI dependency.

## Validation and remaining coverage

The Core suite covers ACMI writing, terrain, labels, and entity lifecycles. The
new lifecycle suite runs the actual production lifecycle against simulated game
state and session boundaries, without loading game assemblies or writing real
recordings. It covers startup without VTOLAPI events, slow readiness, restarts,
exit, unloading, multiplayer suppression, terrain fallback, failures, and retries.

All **201 Core and 45 lifecycle tests pass in Debug and Release (246 each)**;
both mod builds have zero warnings/errors. The prepared ZIP contains exactly the
DLL and manifest. DLL versions and matching staged/ZIP bytes were verified, and
the built DLL has no VTOLAPI assembly or member references.

On 2026-11-07, VTOL VR v1.13.4m1 loaded the local 0.5.2-beta.1 DLL and
finalized a 26-minute, 57-second Target Practice recording despite a VTOLAPI
`GetVehicleEnum()` initialization exception. The ACMI structure and terrain
cache passed verification. The recorder logged no warnings or errors.

Carrier Landing Practice on Akutan, restart/quickload, interrupted terrain
export, and multiplayer suppression remain live test targets. See [TESTING.md](../TESTING.md).

Single-player scope, experimental Akutan terrain, sampled gun bullets, and visual
proxy limitations remain. Sanitize logs and recordings before reporting through
[GitHub Issues](https://github.com/kerimala/vtolvr-tacview-recorder/issues); do not
share generated terrain caches or extracted game assets.
