# Upstream PR #1466 — Read-Only AssetBundle Exploration

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1466](https://github.com/CoplayDev/unity-mcp/pull/1466)  
**Target baseline:** beta 142ea0af4890337f0ff6658df61db23982313717  
**Candidate head:** f25e0c21957c9caf84bcc327909eeb54e166d7a3  
**Status:** Open, not merged  
**Decision:** **ADAPT conditionally (P2)** if the project uses AssetBundles.  
**Method:** Source/test inspection only; no Unity Editor or tests were run by this review.

## Executive assessment

PR #1466 adds two read-only actions to the existing manage_asset family:

- **list_asset_bundles** — list subdirectories and files with UnityFS, UnityRaw or UnityWeb bundle signatures in a selected directory.
- **get_asset_bundle_info** — inspect asset/scene path names, counts, bundle name, file size and streamed-scene metadata without loading assets or opening a scene.

This is distinct from AssetDatabase search: it inspects already-built output files, which may not be imported as ordinary project assets. It is a useful diagnostic if a game build pipeline ships AssetBundles; otherwise it is optional and should stay behind higher-value generator/Editor reliability work. It is in scope for MCP↔Unity handling and does not depend on external asset generators.

## 1. Source-verified behavior and safeguards

The candidate implementation is [AssetBundleExplorer.cs](https://github.com/CoplayDev/unity-mcp/blob/f25e0c21957c9caf84bcc327909eeb54e166d7a3/MCPForUnity/Editor/Helpers/AssetBundleExplorer.cs).

### Path boundary and symlink handling

The handler resolves relative paths under the Unity project root, canonicalizes with Path.GetFullPath and checks path-boundary containment instead of a raw string-prefix check. It walks path segments for reparse points/symlinks, rejects them and skips linked entries in directory listings. Preserve those checks before opening any file.

### Header sniffing before loading

The listing checks file length and reads only the initial bytes to identify the three Unity bundle signatures. It excludes empty/short files before reading, which avoids blocking on a zero-length Unix FIFO that would otherwise wait for a writer. A recognized header is only a candidate indicator: it does not prove the full bundle can be loaded.

### Inspection is metadata-only

The inspect action discovers UnityEngine.AssetBundle by reflection, avoiding a static dependency on the optional AssetBundle module. It uses LoadFromFile, GetAllAssetNames and GetAllScenePaths, then unloads only the handle it opened with Unload(false) in a finally block. It does not call LoadAsset or open the listed scenes. A failed LoadFromFile returns an explicit error; an already-loaded bundle must not be unloaded or altered.

### Response bounds and preflight

Page size is limited to 1–200, page number must be positive, and page offsets use long arithmetic. The results are sorted and include totalEntries and nextPageNumber. The Python wrapper uses the standard no-compile preflight but disables AssetDatabase refresh for these actions, which is appropriate when reading existing build files.

The candidate adds Python schema/forwarding tests, CLI forwarding/limit tests and Unity EditMode tests for signatures, paging, outside-project paths, symlinks, empty named pipes, bundle contents, existing loaded bundles, and preserving existing scene counts/assets.

The PR author reports 1,589 Python tests passing with 18 skipped, 17 related EditMode tests passing on Unity 6000.3.20f1 / Test Framework 1.6.0, and a package import/compile check. This is author-reported evidence, not an independent run. The PR body notes several Unity-version matrix entries were unavailable and the PR remains open/unmerged.

## 2. Performance boundary

The response is page-bounded, but the work is not bounded by page size. The list implementation enumerates every entry in the selected directory, inspects attributes and may read the header of each file, materializes the complete result list, sorts it, and only then slices the requested page. Thus pageSize=25 limits the response payload but still incurs O(N) directory work and storage for N entries.

This is likely acceptable for ordinary bundle output directories, but large build folders can still stall the Editor or allocate more memory than the response size suggests.

**Recommendation:** keep the first implementation small, but measure it on representative output folders. If counts are high, add an explicit scan budget or an asynchronous/cursor-backed path using existing job conventions. If exact total count is retained, make the full-scan cost/completeness clear. Do not add a new job framework solely for this action.

## 3. Adoption constraints

1. Treat these actions as inventory/metadata inspection, not full bundle integrity validation; Unity's LoadFromFile remains the integrity/build-target check.
2. Preserve the explicit missing-module and reflective failure paths.
3. Keep the already-loaded bundle and active scene untouched. Fail if a selected bundle is already loaded rather than unloading it.
4. Do not import bundle files into AssetDatabase just to enumerate their contents.
5. Enforce path and paging rules at the Unity handler, even when the CLI is bypassed.
6. Keep the distinction between directory-scan cost and response page size explicit.

## Recommended decision

**ADAPT conditionally at P2.** If the target project uses AssetBundles, this is a compact read-only tool to verify build output without loading content or changing the active scene. Reuse the existing manage_asset action family, canonical project-root/symlink policy, explicit page bounds and bundle-handle cleanup. Add a realistic large-directory benchmark or a scan budget before representing pagination as a cost bound.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Directory has ordinary files and valid bundle files | Only subdirectories and recognized bundle signatures are returned in stable order. |
| Path exits the project or has a sibling-prefix escape | Request fails before opening any file. |
| Path contains a symlink/reparse-point segment | Request fails; listing does not follow linked entries. |
| Directory includes an empty named pipe or short file | Listing completes without blocking. |
| Bundle is corrupt or built for another target | Inspection returns a bounded load error. |
| Bundle contains assets and scenes | Names/counts are correct; assets/scenes are not loaded. |
| Selected bundle is already loaded | Request fails without damaging the pre-existing handle. |
| Metadata/reflective call fails | Only a handle opened by this request is cleaned up; other bundles/scenes remain intact. |
| Very large directory | Scan work is measured/capped, or the full scan cost is reported clearly despite response paging. |
| Huge page number | Safe empty page; no integer overflow. |
| AssetBundle module missing | Explicit unsupported response; package compiles without static dependency. |

## Source references

- [PR #1466](https://github.com/CoplayDev/unity-mcp/pull/1466)
- Candidate [AssetBundleExplorer.cs](https://github.com/CoplayDev/unity-mcp/blob/f25e0c21957c9caf84bcc327909eeb54e166d7a3/MCPForUnity/Editor/Helpers/AssetBundleExplorer.cs)
- Candidate [AssetBundleExplorerTests.cs](https://github.com/CoplayDev/unity-mcp/blob/f25e0c21957c9caf84bcc327909eeb54e166d7a3/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/AssetBundleExplorerTests.cs)
- Candidate [manage_asset.py](https://github.com/CoplayDev/unity-mcp/blob/f25e0c21957c9caf84bcc327909eeb54e166d7a3/Server/src/services/tools/manage_asset.py)
- Target [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs)
