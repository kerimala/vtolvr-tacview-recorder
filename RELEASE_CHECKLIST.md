# Community-beta release record

## 0.5.2-beta.1 preparation — 2026-10-31

The owner authorized publishing the beta on 2026-11-08 based on the successful
Target Practice flight on VTOL VR v1.13.4m1.

- [x] Remove all runtime/build calls and references to VTOLAPI.
- [x] Use native mission monitoring, early exit finalization, player-actor lookup,
  multiplayer suppression, and bounded failure handling.
- [x] Run 201 Core + 45 lifecycle tests in both Debug and Release (246 each).
- [x] Build Debug and Release with zero warnings/errors and `DeployMod=false`.
- [x] Prepare version metadata, release notes, installation/support documentation,
  and the future Workshop description for 0.5.2-beta.1.
- [x] Verify local ZIP, private package, DLL-only staging, metadata and hashes.
  The ZIP has exactly the DLL and manifest; ZIP bytes match the build/source.
  The built DLL has no VTOLAPI assembly/member references and retains the
  original embedded aircraft-label XML. Installed 0.5.1 DLLs are unchanged.
- [x] Verify Target Practice starts and finalizes despite a VTOLAPI startup exception.
- [ ] Complete the remaining live gameplay matrix in TESTING.md.
- [x] Owner authorizes community beta publication.

Run `tools\PrepareBeta.ps1` to create only local artifacts under
`artifacts\beta\0.5.2-beta.1\`: a private test folder with DLL/manifest/PDB,
Workshop staging with exactly one DLL, and a release ZIP with DLL/manifest plus
SHA-256 checksums. No game assemblies or generated caches belong in the ZIP.

Published on 2026-11-08 as a [GitHub prerelease](https://github.com/kerimala/vtolvr-tacview-recorder/releases/tag/v0.5.2-beta.1)
and an update to [Workshop item 3798566504](https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504).
The GitHub ZIP digest matched the local release package. The Steam download
reported 0.5.2-beta.1 and its DLL matched the locally tested DLL byte for byte.
The public Workshop description and change note show the new version and the
Required Items section is empty. GitHub issue #1 was closed as completed and
PR #2 was closed as superseded, with credit to the reporter.

## Owner decision — 2026-09-09

The owner authorized committing, pushing, a clean public Git history,
publication, and a subsequent Reddit announcement. Publication proceeds as a
community beta with remaining live coverage clearly documented. The owner
accepts the previously discussed permission uncertainty and intends to remove
the mod if the rights holder objects. No further legal approval is pending.

The original repository remains private. Public source uses a new repository
with one reviewed initial commit; the old Git history is not made public.

## 0.5.0 release checks

- [x] Preserve private Git history and current runtime evidence locally.
- [x] Validate the 2026-09-09 Target Practice ACMI and recorder log.
- [x] Document remaining coverage rather than mark untested scenarios passed.
- [x] Re-run Debug/Release tests (195/195 each) and builds (0 warnings/errors).
- [x] Verify release ZIP and Workshop staging contain only intended files.
- [x] Verify clean public source and GitHub release download.
- [x] Upload Workshop item as Hidden and add VTOLAPI as a required item.
- [x] Subscribe/download the Workshop DLL; SHA-256 matches the release DLL.
- [x] Refresh Mod Manager and verify recorder/VTOLAPI discovery and autostart.
- [ ] Run a new in-game smoke test using the downloaded Workshop version.
- [x] Make Workshop item public; verify anonymous access.
- [ ] Draft the Reddit announcement jointly; the owner will post it personally.

## Live coverage still requested

Akutan alignment/startup performance; VTOLCannon/GAU-8 live sampling;
campaign aliases; unknown/custom ship fallback; early exit with pending terrain;
multiplayer suppression; a fresh in-game run using the Workshop installation.
These are open beta validation targets. A known material recording failure
must be investigated before expanding distribution.

## Published distribution — 2026-09-09

- GitHub: https://github.com/kerimala/vtolvr-tacview-recorder/releases/tag/v0.5.0-beta.1
- Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504
- Workshop screenshot: owner-provided Tacview playback image, uploaded and visually checked.
- Required item: VTOLAPI (`3265689427`).
- Workshop DLL SHA-256: `8a698e0b8c2d871f133dfbf39f6fc1143c4b796ec622c74c37155701b93680dd`.
- The previous private recorder autostart was disabled; Workshop recorder and VTOLAPI autostart enabled.

Download integrity and Mod Manager discovery are verified. The earlier successful
flight was from the private test build; no new Workshop runtime test is claimed.

## 0.5.1-beta.1 update — 2026-09-13

The owner authorized committing, pushing and updating GitHub and the existing
public Workshop item. Fresh EF-24G gameplay is delegated to community testers
because the maintainer does not own the DLC. No fresh DLC flight is claimed.

- Bundled label definitions were visually checked against two original EF-24G
  recordings in Tacview 1.9.5.201, with the existing F-14 model retained.
- Automated validation and distribution checks are recorded with the release.
- Game mod-load integration and fresh EF-24G gameplay remain open testing targets.

### Verified distribution

- Public source/tag: `6c7561d212706d4c976222eaf0c0f0606dd3fbd7`, `v0.5.1-beta.1`.
- [GitHub prerelease](https://github.com/kerimala/vtolvr-tacview-recorder/releases/tag/v0.5.1-beta.1)
  is published. Downloaded ZIP entries and DLL bytes match the local package.
- Existing [Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3798566504)
  updated successfully; public description and change note show 0.5.1-beta.1.
  Public visibility, Client Side tag, preview image and VTOLAPI requirement retained.
- Steam downloaded the update; the subscribed Workshop DLL matches the GitHub DLL.
- DLL SHA-256: `3d9dd693d690a0293605ef475b654502d742640c469931def2c42837ca8a6f66`.
- ZIP SHA-256: `b4847aa6fc7477370461dfa57ad1bc9da9868f7a9648b6e8b0dce143b46d1073`.
- 201/201 tests passed in Debug and Release; both builds had 0 warnings/errors.
- Release DLL installer passed Installed/Current/Updated checks on Windows CLR 4;
  installed definitions matched the source resource.
- Gitleaks scans of the complete public history, source and downloaded package
  found no indications of secrets. This is not an absolute guarantee.
- No new game session was run; fresh gameplay and game mod-load validation remain open.
