# Community-beta release record

## Owner decision — 2026-09-09

The owner authorized committing, pushing, a clean public Git history,
publication, and a subsequent Reddit announcement. Publication proceeds as a
community beta with remaining live coverage clearly documented. The owner
accepts the previously discussed permission uncertainty and intends to remove
the mod if the rights holder objects. No further legal approval is pending.

The original repository remains private. Public source uses a new repository
with one reviewed initial commit; the old Git history is not made public.

## Next release

- [ ] Remove VTOLAPI from the Workshop item's required items.
- [ ] Verify a clean install does not download or load VTOLAPI.
- [ ] Run mission start, reload, normal exit, and multiplayer-suppression smoke
  tests using the native mission-state monitor.

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
