# Support and feedback

GitHub Issues is the primary structured feedback channel for this project.
Reddit comments and other community discussions are useful for discovery and
announcements, but they are not the canonical triage queue. There is currently
no public support e-mail address; do not invent or guess an address and do not
send private logs to an unverified account. This project has no automatic
feedback upload or telemetry channel.

## Which form to use

- Use the [bug report form](https://github.com/kerimala/vtolvr-tacview-recorder/issues/new?template=bug_report.yml) for broken behavior, compatibility problems, installation/documentation problems, and missing or incorrect recording output.
- Use the [feature request form](https://github.com/kerimala/vtolvr-tacview-recorder/issues/new?template=feature_request.yml) for a new capability or workflow improvement.
- Search existing issues before opening a new one. Keep one problem or proposal per issue and include the exact recorder, VTOL VR, Mod Loader, and Tacview versions.

Reddit comments and other community discussions are welcome for discovery and
announcements, but GitHub Issues are the canonical place for a report that
needs triage. If a
useful report starts elsewhere, please bring the reproducible details and
sanitized evidence into one GitHub issue rather than posting the same report in
multiple places.

## Evidence checklist

For a bug, the issue form asks for the mission/map, reproduction steps,
expected and actual behavior, and evidence from:

- `Player.log`, normally at `%USERPROFILE%\AppData\LocalLow\Boundless Dynamics, LLC\VTOLVR\Player.log`
- the finalized single-player `.acmi`, normally under `%USERPROFILE%\Documents\Tacview\VTOL VR`
- a Tacview screenshot or short recording showing the result

Attach only the smallest relevant excerpt or file. Include an approximate
mission time when the behavior is visible only during a particular event.

## Privacy and redistribution rules

Before uploading anything, remove or redact Windows usernames, full local
paths, tokens, keys, credentials, chat, and unrelated personal information.

Never attach:

- generated `VTOLVR.*.raw` or matching terrain cache files from `%APPDATA%\Tacview\Data\Terrain\Custom\`;
- extracted VTOL VR game assemblies, models, textures, maps, or other game assets;
- recordings or screenshots containing other players' multiplayer data; or
- credentials, API keys, Steam session data, or private account information.

The recorder is single-player only. Multiplayer recording is intentionally
suppressed, so reports should describe a single-player mission unless they are
specifically documenting that suppression. See
[LEGAL_AND_PRIVACY.md](LEGAL_AND_PRIVACY.md) for the distribution and EULA
boundaries.

## Before reporting a known limitation

Please read the [beta release notes](docs/RELEASE_NOTES_0.5.2_BETA.md),
especially the notes about local heightfield terrain, visual proxy shapes,
sampled pooled gun bullets, Tacview restart/Auto Terrain Switch, and
single-player scope.

The older 0.5.1-beta.1 requires VTOLAPI. The 0.5.2 beta
removes that dependency to address missing recording startup when VTOLAPI fails
to initialize. Include the exact recorder version in reports. Target Practice
passed a recording smoke test. Carrier Landing Practice on Akutan remains untested.
