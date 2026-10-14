# Asset Dependency and Reference Analysis — Target Gap and Safe Design

**Review date:** 2026-10-14  
**Target:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp), branch **beta**  
**Target baseline:** **142ea0af4890337f0ff6658df61db23982313717**  
**Alternative reviewed:** [ozankasikci/unity-editor-mcp](https://github.com/ozankasikci/unity-editor-mcp), branch **main**  
**Decision:** **ADAPT** the capability, not the reference implementation.  
**Runtime validation:** not run; this is source-level analysis and a proposed test plan.

## 1. Executive conclusion

A project-local asset dependency inspector is a real capability gap in the inspected target. The current **ManageAsset.GetAssetData** response includes path, GUID, type, name, folder flag, instance ID, timestamp and optional preview, but its final comment still says **TODO: Add more metadata? Importer settings? Dependencies?**. The existing action surface can inspect, search and modify assets; it does not expose a dedicated, bounded dependency/reverse-reference graph query.

Ozan Kasikci's implementation demonstrates a practical Unity-side action set: direct/recursive dependencies, reverse dependents, size estimates, cycle analysis, potential-unused candidates and reference validation. Its implementation also demonstrates why it should not be copied wholesale: dependency depth is simplified to two levels, reverse-reference search scans all assets and queries each one's dependencies, cycle analysis is restricted/simplified, and serialized AssetDatabase relationships are treated too confidently as “usage.”

**Recommended next step:** retain P2.2 in the backlog and design a dedicated read-only **analyze_asset_dependencies** capability backed by Unity's AssetDatabase. Return precise graph semantics, explicit scan scope, stable pagination and conservative classifications. Do not automatically delete assets, and do not label an asset “unused” based only on the absence of serialized asset-file edges.

## 2. What the target currently does

### 2.1 Asset metadata

Target source: [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), blob **69aaf53cc93a0b638d5a10139f4751dc42c13787**.

The inspected **GetAssetData** method near lines 1034–1120:
- obtains an asset GUID with **AssetDatabase.AssetPathToGUID**;
- determines the main type with **AssetDatabase.GetMainAssetTypeAtPath**;
- loads the main asset and returns basic identity/timestamp data;
- optionally renders/encodes a PNG preview into Base64;
- leaves dependency/importer metadata as a TODO.

The search implementation near lines 685–720 calls **AssetDatabase.FindAssets**, converts GUIDs to paths and calls **GetAssetData** for each match before applying **Skip/Take** pagination. This is already the subject of a separate upstream pagination review. A dependency analyzer should not repeat this eager asset-hydration pattern; it should return concise path/GUID/type graph rows and page or store larger scan results.

### 2.2 Existing tests

The inspected **TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools** directory listing contains many focused tool tests, but no clearly named asset dependency-analysis test suite was apparent. That is not proof no related test exists elsewhere; it does mean a new graph feature should be accompanied by targeted EditMode tests rather than relying on general ManageAsset smoke coverage.

### 2.3 What not to duplicate

Do not add dependency information to every **GetAssetData** response by default. Dependency expansion can be much larger and more expensive than basic asset metadata, especially for scenes, prefabs and model/material chains. Keep normal asset listing compact and make graph expansion an explicit action with scope, direction, recursion and response-budget parameters.

## 3. Alternative implementation source review

Primary source: [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs), blob **439239aec5f2562eb0866fd84fd5e3dafa2e668e**. Existing notes also identify the MCP-facing [AssetDependencyToolHandler.js](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/src/handlers/asset/AssetDependencyToolHandler.js), blob **2c94da8b497c7c98f7a4b79a2c2fc6e3ec7d922**, and [handler unit tests](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/tests/unit/handlers/AssetDependencyToolHandler.test.js), blob **9c938bdf4c7009d8768f219b6402064d26aa37e6**.

| Area | Observed implementation | Assessment |
|---|---|---|
| Direct/transitive dependencies | Calls AssetDatabase.GetDependencies(path, recursive). For recursive output it labels any non-direct dependency as depth 2. | The API provides a useful edge list, but depth values are incorrect for graphs deeper than two edges. Do not copy the depth field without computing real graph distance. |
| Reverse dependents | Finds all asset GUIDs, converts each to a path, then calls GetDependencies(candidate, false) to see whether it contains the requested path. | Correctly expresses the basic reverse-edge question, but O(N) dependency queries are expensive. The implementation needs a measured scope and a graph/index strategy before being exposed for large projects. |
| Circular analysis | Searches only script assets, caps the initial scan to 100, and follows .cs edges with a simplified DFS/path list. | Not a general cycle analyzer. The cap can omit cycles and the traversal does not provide a robust graph-wide strongly connected component result. Do not report “no cycles” for an incomplete search as proof none exist. |
| Potential-unused assets | Builds a set of assets referenced by direct AssetDatabase edges, then reports assets not in that set; scripts and scenes are excluded. | Useful as a first candidate heuristic, not a proof of non-use. Runtime string loads, Addressables, Resources, build settings and external content conventions may not appear as ordinary serialized asset references. |
| Size impact | Adds file-size estimates for an asset and its recursive dependency set. | Can estimate source-file footprint only where file metadata is available. It is not equivalent to compressed build size, runtime memory, duplicated bundle size or total deployed bytes. |
| Validation evidence | The cited Node-side tests use a mocked Unity connection. | They exercise request validation/forwarding, not the correctness of Unity AssetDatabase graph semantics. Unity EditMode tests are required. |

This review confirms the capability is feasible while ruling out the main assumptions that would otherwise create false confidence.

## 4. Unity API semantics and limits

### 4.1 Forward dependencies

Unity's [AssetDatabase.GetDependencies documentation](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetDependencies.html) says that the method returns asset paths referenced by the given path(s). The recursive flag selects direct edges versus transitive results. When recursive is true, the result includes the input asset path itself; normalize that self entry out of the dependency list while preserving a separately reported root ID. Unity also notes these references are not necessarily required during the build process.

For accurate depth, use the direct-edge mode and build a graph traversal (for example breadth-first search) from the requested root, tracking the minimum or first-discovered distance per normalized asset path. If only a recursive flat set is requested, do not fabricate depth—omit the field or return a clearly named “depth not computed” state.

### 4.2 Reverse dependents

The public API provides a forward dependency query; a reverse index has to be assembled from a defined candidate set or maintained cache. The naive full-project scan is simple but can be slow. The initial implementation should make scope explicit, such as **Assets** by default, with **Packages** included only when requested, and expose:
- how many assets were scanned;
- the included roots/folders and exclusions;
- whether the reverse map came from a fresh scan or cached index;
- whether results are complete or capped;
- duration and truncation/continuation metadata.

Do not infer completeness from a partial scan. If the graph must be rebuilt, return a pollable job/continuation handle or a clear timeout/backpressure status instead of blocking the Editor indefinitely.

### 4.3 Dependency hash and invalidation

Unity's [AssetDatabase.GetAssetDependencyHash documentation](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetAssetDependencyHash.html) says the method returns an aggregate dependency hash for an asset, including source path/source asset/meta file/target platform/importer version. This can help detect changes relevant to a root's forward dependency state.

It is **not sufficient on its own for reverse-index invalidation**: adding or changing another asset that references a target may alter the reverse map without changing the target's own forward dependency hash. A graph/index must be invalidated or reconciled on imported, deleted, moved and moved-from assets; package/build-setting changes that affect the selected universe; relevant script/domain reloads; and explicit refresh or project identity change. If the feature uses a cache, state its invalidation policy and show whether returned data is fresh or cached. A cache without invalidation evidence is worse than a slow, clearly labelled scan.

### 4.4 “Unused” and reference validation

An AssetDatabase dependency graph answers a limited question: *which asset files does Unity's AssetDatabase currently report as referenced by other files inside the scanned universe?* It does not guarantee completeness for runtime usage conventions such as:
- string-based paths or keys in code/configuration;
- Resources.Load path conventions;
- Addressables keys/groups and runtime catalog loading;
- build scenes, preloaded assets and other build configuration;
- dynamic content downloaded/imported by a project's own runtime;
- references from external tools, mods or content pipelines.

Therefore use labels such as **noSerializedAssetDependentsInScan** or **potentiallyUnreferenced**, not **unused=true**. An optional validation action may report missing GUID/path references discoverable from Unity data, but must describe which reference classes it checks. An unused candidate must never trigger automatic deletion.

### 4.5 Size estimates

A result can sum unique asset-file byte sizes for a dependency closure when files are accessible. It must identify inaccessible/built-in/package files and state whether it reports direct file size, unique source-file dependency footprint, or a separately verified build-size measure. Do not conflate source sizes with build output, AssetBundle size, compression, duplicate packaging, or resident memory.

## 5. Proposed capability contract

Prefer an explicit read-only tool such as **analyze_asset_dependencies**, separate from general asset CRUD/search. Use a small typed action set and a stable, bounded result envelope.

| Operation | Contract | Required caveats |
|---|---|---|
| dependencies | Return direct edges or a recursive closure for one root. | Recursive closure includes/root-self is normalized; true depth only when actually traversed. |
| dependents | Return scanned assets with a direct edge to the target; optionally support transitive dependents as a separate explicit mode. | Must disclose graph scope, scan completeness, freshness and cost. |
| validate_references | Validate reference classes the implementation can prove from Unity/AssetDatabase data. | No claim that string/runtime/addressable references are covered unless separately implemented and tested. |
| size_impact | Report direct file size and unique dependency-closure file-size estimate. | Excludes or labels unknown/inaccessible files; no claim of exact build size or runtime memory. |
| potentially_unreferenced | Page assets with no detected dependents inside the selected graph. | Heuristic only; include exclusions, confidence/evidence and warning; never auto-delete. |

Suggested common parameters: **asset_path** for a root-specific query, action/direction, recursive, **scope_folders** or an explicit scope preset, **include_packages**, **page_size**, **cursor**, optional **include_file_sizes**, and an explicit **max_scan_assets** or deadline. Do not force all parameters onto every call if action-specific schemas can stay smaller. Keep default output to summary + first page; use stable continuation handles for large reports.

For costly global reverse-dependency and candidate scans, use the existing async-operation conventions if they satisfy the need. Do not add a second job manager; first map current job, cancellation and continuation contracts. Scan work should be cancellation-aware, emit progress in bounded intervals, and avoid repeated main-thread UI repaint or redundant whole-project scans.

## 6. Graph and algorithm requirements

1. Normalize identity using asset path/GUID pairs. A move should update the displayed path without confusing it with a different asset; missing GUID paths are skipped and counted explicitly.
2. Define the asset universe before scanning: for example Assets by default, with package folders opt-in. Explain whether folders and non-main sub-assets are nodes, and keep identity semantics consistent.
3. Direct graph edges should come from Unity's non-recursive dependency query; deduplicate and deterministically sort results.
4. For real depth and cycle detection, traverse the direct graph with visited/active states. For all cycle groups, use a proper strongly connected component algorithm or explicitly constrain and name the analysis.
5. For reverse graph queries, either build/cache a reverse adjacency map from forward edges or scan a bounded candidate universe. Do not call GetDependencies once per candidate without scan limits, progress and benchmarks.
6. If caching, version it by project and scope, increment an invalidation generation for changed paths and structural changes, and label stale/in-progress state. A forward dependency hash is one signal, not the complete reverse-index invalidation mechanism.
7. Keep the entire analysis read-only. Results may guide human review, not perform deletions or automatic asset moves.

## 7. Proposed EditMode fixtures and acceptance criteria

### Graph correctness

- Root with zero dependencies.
- Root with one direct dependency.
- A → B → C → D chain: direct mode returns only B; closure returns B/C/D; true depths are 1/2/3.
- Diamond graph (A→B/C, B/C→D): D appears once in the closure with a documented depth rule.
- Shared dependency used by two prefabs/scenes.
- Cycle or strongly connected fixture where Unity can represent it; distinguish unsupported asset-cycle assumptions from a completed zero-cycle result.
- Recursive query does not return the root as its own dependency.
- Missing/moved GUID paths, duplicate/case-sensitive path normalization as supported, folder paths, embedded packages and read-only package assets.

### Reverse graph and invalidation

- Reverse scan returns only assets with direct edges to the target.
- Moving/renaming an asset preserves GUID identity and updates its path.
- Import/delete/move/moveFrom changes invalidate the reverse index correctly.
- Adding a new dependent invalidates the target's reverse map even if the target's own forward dependency hash is unchanged.
- Domain reload, package/scope change, project switch and build-settings change cannot reuse an incompatible cache.
- Scan cap/deadline/cancellation returns explicit partial/incomplete metadata with no false “complete” signal.

### Result contract and safety

- Stable ordering, page boundary/no duplicates, opaque cursor validation and output-byte caps.
- Very large project is paginated; only requested page metadata is materialized.
- Dependency-file sizes sum unique paths once; missing/unavailable files are counted and marked unknown.
- Potentially-unreferenced results include a warning and defined scan universe; no delete or move action is ever invoked.
- Asset paths stay within permitted project/package scope; output never leaks absolute machine paths unless that is an explicitly supported diagnostic.
- Cache hit/miss/freshness and scan duration are visible in metadata.

### Performance evidence

Benchmark a synthetic and a real large project separately. Record asset count, edge count, selected scope, cold and warm duration, number of GetDependencies calls, peak allocations if available, result bytes and Editor responsiveness. Do not claim that an implementation is faster merely because a single test asserts it returned a page. Compare: direct root query, closure query, cold reverse-index build, warm reverse lookup, invalidation rebuild, and cancelled global scan.

## 8. Adoption decision

**ADAPT**, with a narrow first version:
1. forward direct/recursive dependency query with accurate bounded output;
2. reverse dependents within an explicit scope, with measured scan behavior;
3. conservative reference diagnostics and unique-file-size estimate;
4. potentially-unreferenced reports only after graph completeness, cache invalidation and false-positive tests are satisfactory.

Defer global cycle analysis and strong “unused asset” claims until the graph model and test coverage prove them. Do not copy Ozan's depth labels, simplified cycle walk, or whole-project reverse scan as-is.

## Sources

- Target [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), blob **69aaf53cc93a0b638d5a10139f4751dc42c13787**.
- Alternative [AssetDependencyHandler.cs](https://github.com/ozankasikci/unity-editor-mcp/blob/main/unity-editor-mcp/Editor/Handlers/AssetDependencyHandler.cs), blob **439239aec5f2562eb0866fd84fd5e3dafa2e668e**.
- Alternative MCP request handler [AssetDependencyToolHandler.js](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/src/handlers/asset/AssetDependencyToolHandler.js), blob **2c94da8b497c7c98f7a4b79a2c2fc6e3ec7d922**.
- Alternative request/forwarding tests [AssetDependencyToolHandler.test.js](https://github.com/ozankasikci/unity-editor-mcp/blob/main/mcp-server/tests/unit/handlers/AssetDependencyToolHandler.test.js), blob **9c938bdf4c7009d8768f219b6402064d26aa37e6**.
- Unity [AssetDatabase.GetDependencies](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetDependencies.html) documentation: recursive results include indirect dependencies and the root itself; reported dependencies are file references and are not necessarily build requirements.
- Unity [AssetDatabase.FindAssets](https://docs.unity3d.com/ScriptReference/AssetDatabase.FindAssets.html) documentation: returns GUIDs and scoped folder arguments constrain and speed search.
- Unity [AssetDatabase.GetAssetDependencyHash](https://docs.unity3d.com/ScriptReference/AssetDatabase.GetAssetDependencyHash.html) documentation: aggregate hash includes source path/asset, meta file, target platform and importer version.
