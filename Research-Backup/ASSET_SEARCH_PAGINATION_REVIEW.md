# Asset Search Pagination — Source Review and Acceptance Plan

**Review date:** 2026-10-14  
**Target:** ApexForge-devz/unity-mcp, beta, commit 142ea0af4890337f0ff6658df61db23982313717  
**Upstream proposal:** [CoplayDev/unity-mcp PR #1459](https://github.com/CoplayDev/unity-mcp/pull/1459)  
**Observed PR state:** open, last updated 2026-10-13 in the source snapshot.  
**Decision:** ADAPT the page-only hydration and fail-fast input contract; keep the performance claim narrow until benchmarked.  
**Validation:** source/patch/test review only; no Python or Unity tests and no performance benchmark were run here.

## 1. Executive conclusion

The target's asset search computes the page only **after** building metadata for every matching asset. For each match it resolves the GUID into a path, optionally stats the file for the date filter, and calls GetAssetData; that method loads the main asset and may also create a readable texture copy, encode a PNG and produce a Base64 preview. Only after all matches have been hydrated does the target call Skip/Take and discard the records outside the requested page.

This turns a paginated response into an unpaginated metadata/preview workload. With generatePreview enabled, loading and encoding previews for discarded entries can dominate the task and response preparation. It also makes the response path allocate a result object for every match before trimming it.

PR #1459 improves the expensive part of that loop by calling GetAssetData only for the requested page, while still counting all matching results. It also rejects empty/invalid folder scopes rather than silently falling back to a project-wide search, validates positive pageSize/pageNumber, uses long arithmetic for the offset product, and adds EditMode tests that assert off-page assets remain unloaded.

**Important limit:** this is not a constant-time search and does not bound all scan memory. AssetDatabase.FindAssets still returns the full GUID array, and the implementation still visits each candidate to count matches and apply a date filter. The defensible claim is that it reduces per-result asset hydration and preview generation to the returned page. A separate measured change would be needed to avoid the full GUID list or whole-scope count pass.

## 2. Target implementation verified

Target source: [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), blob **69aaf53cc93a0b638d5a10139f4751dc42c13787**. The relevant SearchAssets method was fetched around lines 635–730; GetAssetData was inspected around lines 1034–1120.

### Current flow

1. Reads searchPattern, filterType, path scope, optional date filter, pageSize (default 50), pageNumber (default 1), and generatePreview.
2. If path is non-empty but is not a valid folder, emits a warning and sets folderScope to null. AssetDatabase.FindAssets then searches outside the intended scope—potentially the entire project.
3. Calls AssetDatabase.FindAssets once to produce a string array of all matching GUIDs in the search scope.
4. Iterates every GUID, resolves it through GUIDToAssetPath, applies the optional filesystem modification-date filter, increments totalFound and calls GetAssetData for every match.
5. After completing the scan, computes the page offset and applies Skip/Take to the already-created metadata list.

### Why this costs more than the response size suggests

GetAssetData performs more than path formatting: it resolves the main type, calls LoadAssetAtPath, obtains a main object instance ID and may produce a readable temporary texture and Base64 PNG preview. Therefore an output page of 25 rows can trigger hundreds or thousands of asset loads/previews if the query finds many assets.

There is also an incorrect operational fallback: a typo or file path supplied as the folder scope silently broadens the search instead of producing an actionable error. That can unexpectedly increase scan time and result volume, even though the caller intended a local folder query.

### Python-side context

Target [manage_asset.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_asset.py) exposes path as the scope and recommends putting query text in search_pattern. It already has a compatibility shim: for a search whose path starts with t: and lacks search_pattern, it moves the query into search_pattern and sets path to Assets. It also maps asset_type to filter_type where appropriate. That narrow compatibility behavior can remain; it does not justify a generic invalid-folder fallback in the Unity handler.

Although path is required by the MCP schema, an empty string can still be supplied; direct/batch Unity calls also bypass the Python schema. Validate the effective scope at the Unity boundary.

## 3. What upstream PR #1459 changes

The patch to ManageAsset.cs contains four substantive improvements:

- **Page-only hydration:** increments the total matching count for all eligible candidates, but calls GetAssetData only when the candidate index lies within the requested page and that page has room.
- **Strict folder scope:** a blank path returns a clear error. A non-empty invalid folder no longer becomes null folderScope and therefore no longer searches the whole project by accident.
- **Pagination validation:** pageSize and pageNumber must both be greater than zero.
- **Offset overflow safety:** computes the page offset with long arithmetic before multiplying pageNumber by pageSize, preventing an overflowing int product from turning an extreme page into an unexpected range.

The patch also adds [ManageAssetSearchTests.cs](https://github.com/CoplayDev/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/ManageAssetSearchTests.cs) with fixtures that create eight TextAsset files and verify:
- second-page search returns the expected count but loads only those two page assets;
- last-page search returns only the remaining two assets and loads only two;
- a page beyond the end loads none and handles extreme positive page values;
- a date-filtered search counts the filtered matches and hydrates only the requested page;
- zero/negative page values fail without loading assets;
- invalid, empty and missing scopes fail without expanding to project-wide search.

These are useful behavioral tests because they assert actual Unity asset load state, not just the length of the returned array. The tests are present in the patch, but were **not run in this research session**.

## 4. Complexity and the remaining bound

For N matching candidate paths and page size P:

| Work | Target baseline | PR #1459 design |
|---|---|---|
| Materialize FindAssets GUID array | O(N) | O(N) |
| Iterate matches to determine exact total count | O(N) | O(N) |
| Resolve GUIDs and apply date filter when requested | Up to O(N) | Up to O(N) |
| Call GetAssetData / load main object | O(N) matches | At most P matching page entries |
| Generate previews when requested | Up to O(N) | At most P matching page entries |
| Retain metadata row objects before response | O(N) | O(P), aside from the GUID array and result bookkeeping |
| Return response rows | O(P) | O(P) |

This distinction matters for very large Unity projects: the patch substantially reduces Unity object loads, metadata extraction, temporary texture allocation and PNG/Base64 generation, but the full GUID array and exact-count pass remain. Calling this “constant-time pagination,” “only scans the page” or “bounded search” would be inaccurate.

A later optional optimization could make exact totals explicit/optional. If the caller does not require a total count, the handler could stop once it has found enough items for a requested page, subject to a deterministic ordering and correctly applying filters. Another route would be an asynchronous project index. Either approach adds contract/cache complexity and should be benchmarked before adoption; do not infer that it is needed merely from this PR.

## 5. Input contract and edge conditions

### Scope must be intentional

- Preserve the MCP shim that maps an obvious t: query accidentally placed in path into search_pattern and sets path to Assets.
- Validate that the effective folder path exists in the Unity handler before FindAssets.
- Return a clear error for empty/whitespace/invalid path rather than widening to the entire project.
- Make the expected role explicit: path is a folder scope (e.g. Assets or Assets/Characters); search_pattern contains name/query terms; filter_type means an AssetDatabase type filter.

A compatibility shim should be narrow enough not to reinterpret legitimate paths or unrelated strings. An invalid path should not silently turn into a global search just because a client omitted one field.

### Pagination invariants

- Validate pageSize > 0 and pageNumber > 0 before scanning.
- Calculate the offset product in a type wide enough for the largest valid inputs, and avoid narrowing to int before validating the range.
- Keep page numbering explicitly 1-based if that is the existing public contract.
- The total count must reflect the same filtered candidate set from which page rows are selected. Date filtering needs to occur before advancing the logical result index.
- A page beyond the end should return an empty assets array and accurate total; it should not hydrate assets.
- Preserve deterministic ordering from the underlying search. If Unity does not guarantee ordering, document the result order or sort the GUID/path list before deriving pages; sorting itself still costs memory/time.

### Preview and output handling

- Do not generate preview data for rows that will not be returned.
- Keep generatePreview opt-in and default false, as the MCP description already recommends.
- Record result bytes, preview bytes and page size in benchmarks. Base64 may expand the encoded payload substantially beyond PNG bytes.
- Consider a shared output cap later, but do not stack a second pagination mechanism inside the response if the returned page already follows the declared budget.

## 6. Recommended decision

**ADAPT and adopt the semantics if PR #1459 remains unmerged when implementation starts**, or confirm the fix has landed before implementing a duplicate.

Minimum in-scope change:
1. Page-only calls to GetAssetData; do not load or preview off-page assets.
2. Fail-fast invalid folder scope; no implicit project-wide fallback.
3. Validate positive paging values and use overflow-safe offset math.
4. Add EditMode tests that verify loaded asset state, not only response row count.

Do not copy the code without rechecking current target/upstream diffs. PR state was open in the 2026-10-14 snapshot; before implementation, re-query the latest state and inspect the exact merged beta source.

## 7. Required additional performance measurements

Run a synthetic fixture and a real large project with previews disabled and enabled. Capture:

- candidate match count, page size, requested page, scope and filter combination;
- time to AssetDatabase.FindAssets, total scan time, and response serialization time;
- GetAssetData invocation count and assets newly loaded from AssetDatabase;
- AssetPreview and texture-encoding count/time when preview is enabled;
- managed allocations and peak memory where available;
- response bytes, number of Base64 preview bytes, and GC allocation;
- cold/warm behavior before and after an import/domain reload;
- invalid-scope and extreme-page latency, ensuring neither triggers whole-project asset hydration.

Acceptance target: for a result set with N matches and page size P, GetAssetData invocations and new main-asset loads are at most min(P, max(0, N - offset)); they are zero for a page beyond the end. Total GUID scanning can remain O(N), but this limitation must be visible in documentation. Compare latency and allocations on the same project/revision; do not use test success alone as a performance benchmark.

## Sources

- Target [ManageAsset.cs](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/ManageAsset.cs), blob **69aaf53cc93a0b638d5a10139f4751dc42c13787**.
- Target [manage_asset.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_asset.py), source inspected at target beta.
- Upstream [PR #1459](https://github.com/CoplayDev/unity-mcp/pull/1459), title “fix(asset): load only the requested search page and reject invalid search folders.”
- Proposed [ManageAssetSearchTests.cs](https://github.com/CoplayDev/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/ManageAssetSearchTests.cs) and its companion .meta, part of the PR patch.
- Unity [AssetDatabase.FindAssets documentation](https://docs.unity3d.com/ScriptReference/AssetDatabase.FindAssets.html), including the optional folder scope.
