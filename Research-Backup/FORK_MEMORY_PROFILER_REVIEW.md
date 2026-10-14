# Specialist Fork Review — Unity MCP Memory Profiler

**Reviewed:** 2026-10-14  
**Candidate:** [sabriboughanmi/Unity-MCP-MemoryProfiler](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler), `main`  
**Target:** [ApexForge-devz/unity-mcp](https://github.com/ApexForge-devz/unity-mcp), `beta`  
**Decision:** **INVESTIGATE / ADAPT SELECTIVELY**. The object/reference graph queries are valuable, but the candidate cannot be installed as a drop-in, and current export/diff completeness issues must be fixed before adopting those paths.  
**Review type:** Source, repository tree, and documentation inspection. No Unity Editor, package build or test suite was run during this review.

## Executive assessment

The target's [`MemorySnapshotOps.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MemorySnapshotOps.cs) can capture `.snap` files, list snapshots and compare file-level metadata. It does not expose a memory graph API for searching snapshot records, getting object details, traversing references, finding root paths or computing trustworthy before/after object-level deltas.

The candidate has useful algorithms for those tasks: it opens snapshots incrementally, wraps them in reusable sessions, supports paginated/search/sort queries, exposes object details and references, uses bounded traversal for root paths, and exports/diffs profiles across snapshots. These capabilities could help diagnose memory changes caused by a generated scene (for example, an unexpected set of meshes/textures/materials or objects that remain retained after a test flow).

However, the candidate is built on **IvanMurzak's Unity MCP** (`com.ivanmurzak.unity.mcp` 0.83.0), not this repository's CoplayDev bridge. It also depends on Unity Memory Profiler Editor package 1.1.x and internal `Unity.MemoryProfiler.Editor` APIs. Treat it as a source of algorithms and test cases, not as a package that can be added unchanged.

Most importantly, source review finds a high-impact correctness problem: the public query engine caps returned rows at 500; the session-to-profile and export paths ask for limits such as 999,999 or 50,000 but still receive at most 500, and several export domains are then labelled `Complete`. The same cap can make snapshot comparisons silently incomplete. This must be fixed or surfaced as truncation before trusting profile diffs.

## 1. Relevant target baseline

Target `MemorySnapshotOps` uses reflection to find `Unity.Profiling.Memory.MemoryProfiler` (and older API names), detects `TakeSnapshot` signatures, waits for a callback up to 30 seconds, lists `.snap` files and compares basic file-level metadata. It explicitly notes that detailed object-level comparison requires the Memory Profiler Editor window.

**Gap relevant to this agent:** after a scene generator is edited and rerun, the target cannot currently answer from a `.snap` file: “Which Unity objects grew in count/size?”, “Which asset identity changed?”, “What object references retain this object?”, or “What path leads to a root?”.

## 2. Candidate capabilities worth adapting

### 2.1 Incremental load and explicit sessions

The candidate uses `FileReader`, `CachedSnapshot` and incremental `PostProcess(crawlManaged: true)` work. It returns an operation ID and progress, then a reusable session ID after loading, with explicit close/disposal. This avoids forcing all snapshot analysis into one synchronous tool call.

It limits concurrent loaded sessions to two, appropriate for before/after comparison, and reports snapshot metadata and memory summaries. Preserve this incremental pattern, but enforce the cap against **sessions plus in-progress loads**, not loaded sessions alone. The current `BeginOpenSnapshot` guard checks only `s_Sessions.Count`; multiple open operations started before the first completes can bypass the advertised two-session limit.

### 2.2 Query and detail views

The candidate supports summary categories, native labels/allocations/types/regions, managed types, Unity object types, allocation sites and native call stacks, plus individual Unity and managed objects. Object detail reports session-scoped node IDs, type, sizes, asset path/scene identity where resolvable, reference counts and capabilities. The references API can return incoming or outgoing edges with source/target details and pagination.

These read-only queries are a better fit for targeted diagnosis than expanding the current generic scene validator into a whole-scene dump. For the immediate generator workflow, object-level snapshot analysis is a specialist feature: first use generator assertions, a screenshot and normal targeted queries; open a memory snapshot only when memory growth, retained objects or asset/resource accounting is the question.

### 2.3 Paths to roots are usefully bounded

`PathsToRootsEngine` uses a breadth-first traversal and exposes a hard cap of depth 64, at most 20 returned paths and at most 100,000 visited nodes. The result includes `VisitedNodeCount`, `MaxDepthReached`, `Truncated`, a truncation reason and a no-root indication. This is a good example of explicit budgets for a potentially enormous reference graph.

Retain explicit limits and truncation metadata if adapting it. Never return “no retaining path” as a conclusive result when the traversal was truncated or the snapshot did not contain successfully built root/connection data.

### 2.4 Identity-aware comparison, with confidence

For Unity objects, the candidate tries exact asset GUID + local file ID first, then GUID + type, then path + type, then name + type. It exposes `MatchMethod` and `MatchConfidence`; duplicate name/type candidates are reported as ambiguous instead of silently treated as a certain match.

Keep this confidence distinction. Session-scoped `NodeId` values are not stable cross-snapshot identities. For generated or imported content, persistent asset identity is materially better than matching object names alone. Comparisons should report unmatched objects and ambiguous mappings as such.

## 3. Blocking correctness and lifecycle findings

### 3.1 Hard 500-row cap silently truncates exports and session diffs

In `SnapshotQueryEngine`, `ClampLimit` always returns at most 500 (`limit <= 0` becomes 50). The query functions often build every matching row, sort the whole list, then slice the requested page. So this is **not a computation/memory budget**: a request for 20 rows can still create a row DTO for every allocation/type in the selected domain and sort all of them on Unity's main thread.

Two higher-level paths then rely on these capped results as though they were complete:

- `MemoryProfilerExportEngine.ExportProfile` calls query methods with `Limit = 999999` for summary and domain tables, but those methods return at most 500. The exporter labels several domains `Complete` without checking `TotalCount` or whether the result was truncated.
- `SnapshotDiffEngine.LoadFromSession` repeats the same `Limit = 999999` pattern for summary/types/regions/labels, then requests individual Unity objects with `Limit = 50000`, which also clamps to 500. A diff between two live sessions can therefore compare only a prefix of the underlying data. Separately, Detailed export defaults `maxObjectsPerDomain = 10000`; the returned rows can still be 500 and the current `Rows.Count >= maxObjectsPerDomain` test can incorrectly mark the domain `Complete`.

This is a correctness problem, not just a performance nit: reports can undercount and still claim completeness.

**Required correction before adoption:**
- Return and use `TotalCount`, a truncation flag and domain completeness at every layer.
- Separate **interactive response page limit** from **internal enumeration/export completeness**. Internal export/diff must iterate safely across pages or enumerate source collections directly, with a deliberate work/memory budget.
- For expensive allocation domains, process incrementally/aggregate in chunks or mark the profile explicitly `Truncated`/partial. Avoid constructing one unbounded row list plus sorting the entire domain synchronously.
- Add tests with more than 500 rows for every exported/diffed domain; assert that the last entries appear or that the output explicitly says partial. Assert no domain is marked `Complete` when its source count exceeds returned data.

### 3.2 Operation/session bookkeeping has leaks and a concurrency hole

In `SnapshotSessionManager.BeginOpenSnapshot`, the operation is inserted into `s_Operations` before opening/parsing the file. If `FileReader.Open` fails or setup throws, the method reports `Failed` but leaves the failed operation in the dictionary. The later crawl exception path also disposes the reader/snapshot but leaves the operation record. Completed successful opens and explicit cancellation do remove entries; failed ones should be removed or retained in a bounded terminal-result cache with expiry.

The two-session cap checks only loaded sessions, not active open operations. To make a hard concurrency limit reliable, reserve capacity for both in-progress and loaded snapshots before accepting an open.

`SnapshotCaptureManager` stores each capture operation in a static dictionary and terminal records aren't visibly evicted by the reviewed completion path, so repeated captures can accumulate records until cleanup/domain reload. Native capture cannot be cancelled after it starts; the cancel path throws `Active native capture cancellation is unavailable`. This behavior must be described accurately—do not promise general capture cancellation—and tests should verify the structured outcome and temporary-file cleanup.

### 3.3 Overwrite is not atomic/recoverable

For a capture, when the callback reports success and the path exists, the candidate deletes an existing target file and then moves the temporary capture file into place. If that move fails, the previous good snapshot is already gone. JSON profile export writes directly to the final path with `File.WriteAllText`; a process/IO failure can leave a partial output. The baseline capture code and Frame Debugger candidate have similar “delete then move” patterns.

**Required safeguards:** write to a unique temporary file in the destination directory, flush/close it, validate the result, then replace atomically where supported or keep a backup until final replacement succeeds. A failed operation must preserve the last known-good file. Keep explicit overwrite opt-in and clean temporary artifacts.

### 3.4 Snapshot sessions do not survive domain reload

`SnapshotSessionManager` has an `[InitializeOnLoadMethod]` cleanup path that disposes/clears open snapshots and operations. That makes sense for native/file-reader resource safety, but an open before-profile session must not be expected to survive editing/compiling the generator. Export a versioned baseline profile or preserve the `.snap` file and reopen it after reload; compare saved profiles if both live sessions cannot fit the operational context.

The exported profile is a better cross-reload boundary than a session ID, but only after fixing the completeness problems in §3.1.

### 3.6 The "incremental" work budget and cancellation status contract are not actually bounded end-to-end

The tool `memoryprofiler-advance-open-operation` accepts caller-provided `maxSteps` (default 10), and `SnapshotSessionManager.AdvanceOpenOperation` loops until `stepsTaken == maxSteps` or post-processing completes. There is no upper clamp in the reviewed wrapper or manager. A caller can pass a very large positive value and turn an intentionally incremental, main-thread operation into a single long Editor-blocking call. Negative/zero values are also not validated as meaningful progress requests.

The cancellation status path is contradictory. `CancelOpenOperation` changes the operation to `Cancelled`, disposes its native resources, and removes the operation record from `s_Operations`. A subsequent `GetOpenOperationStatus(operationId)` therefore no longer returns `Cancelled`; the missing-operation branch constructs a `Failed` result saying the operation was not found. The tool method returns the string `"Cancelled"` at the time of cancellation, but the advertised state sequence and later status polling cannot verify that terminal state. Keep a bounded terminal-status cache (with an explicit retention duration/count) or return a terminal receipt directly and make subsequent polls consistent. Do not keep every operation indefinitely just to preserve status.

### 3.7 Caller-supplied filesystem paths need an explicit trust boundary

The candidate documents absolute paths for `filePath`, capture `outputPath` and export `outputPath`. The reviewed capture/export engines normalize explicit paths but do not constrain writes to a dedicated project-owned output directory. Capture completion may delete an existing target before moving the temporary file, while export with `overwrite=true` writes directly to the final path. These operations are intentionally file-writing features, but an MCP-controlled arbitrary absolute path is broader than required for typical project diagnostics.

Default output should remain in a dedicated project-local capture/export directory and all generated filenames should be unique by default. For explicit output paths, either constrain the destination to an allowed root by default or require a clearly documented opt-in/approval path for external locations and overwrites. Always validate the resolved parent path after canonicalization; a string prefix is not a containment test. Keep snapshot reading separate from output write permission.


### 3.5 Memory capture is not necessarily a capture of just runtime Play Mode

Unity's documented API says an Editor invocation captures the Editor or the currently attached Player based on PlayerConnection target; it cannot capture Play Mode in isolation from Editor memory. Memory usage in the Editor can differ substantially from a development Player on the target device. Also, `NativeAllocationSites` and `NativeStackTraces` depend on whether the target build supports collection; requested flags can be ignored when unsupported.

Therefore label the target explicitly (Editor vs connected Player), include Unity/platform/capture flags in metadata, and don't interpret missing unsupported callstack domains as zero allocations. For real game-runtime budgets, capture a development Player running on the intended device where feasible.

## 4. Test evidence and compatibility caveats

The candidate README reports 90 EditMode tests passing on Unity `6000.3.19f1`, with Editor-only support. That is project-reported evidence, not an independent run in this review. The repository's own tree has **no `.snap` fixture checked in**, while `SnapshotLifecycleTests`, `QueryEngineTests` and `DiffEngineTests` explicitly use `MemoryCaptures/test memory 1.snap` and call `Assert.Ignore` if it is absent. Thus snapshot-dependent tests may be skipped in a clean checkout unless that fixture is supplied separately; the headline count alone does not show whether those functional tests ran.

Other boundaries:
- Candidate package manifest specifies Unity `6000.3`, Memory Profiler `1.1.12`, and `com.ivanmurzak.unity.mcp` `0.83.0`. The core uses Memory Profiler Editor types and version-sensitive APIs; require a tested compatibility matrix instead of relying on reflective method names alone.
- Unity says captures from a connected Player may be streamed into the Editor rather than stored on the Player; the callback's success/path semantics differ by target connection. Validate the actual resulting file and target metadata instead of treating one callback boolean as the whole truth. See the official [`MemoryProfiler.TakeSnapshot` API](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/profiling/memory/memoryprofiler/takesnapshot).
- Snapshot files can contain the captured managed heap and be large/sensitive. Keep `.snap` files out of source control; use them as explicit local test artifacts and document retention/deletion. See [Unity's snapshot concepts](https://docs.unity.cn/Packages/com.unity.memoryprofiler@1.1/manual/snapshots-concepts.html).

## 5. Integration plan for the target

1. Keep the current low-level capture/list/metadata actions available. Don't replace them just to match the candidate's API shape.
2. Add a small, optional action family under existing `manage_profiler`: capability/version status; async open/session list/close; basic summary and paged searches; object details/references; bounded paths-to-roots; saved-profile compare.
3. Reuse the candidate's DTO/engine concepts where useful, but port to the target's transport and result/error conventions. Do not add 15 independent top-level MCP tools just because this package has that many; prefer coherent actions under the existing family.
4. Make each action report whether data is complete, partial, unavailable or truncated. Preserve per-row identity/match confidence.
5. Enforce the max-session bound across active operations plus sessions; bound operation records and release native objects on all terminal paths; use bounded `maxSteps`; preserve consistent terminal status receipts; report that an active native capture cannot be cancelled if the public API cannot stop it.
6. Add incremental query/export work, safe atomic file handling, a clear external-path/overwrite policy, and test fixtures before relying on memory comparisons for automated decisions.
7. For generator workflow: save/export baseline before generator source changes; edit and compile the persistent generator; regenerate; capture after-state; then reopen/compare saved profiles. Always close loaded sessions when finished.

## Acceptance tests

| Test | Expected result |
|---|---|
| Export/diff domain contains more than 500 rows | All rows are included or the domain is explicitly marked truncated; it is never falsely reported as complete. |
| Query limit is 20 on a huge allocation table | Result contains at most 20 rows and work/memory is bounded or clearly documented as full-domain scan/sort. |
| Three opens are started before any finishes | Hard active-session/operation cap prevents overcommit. |
| Invalid/corrupt snapshot opens | Operation status is actionable; failed operation record and file handles are cleaned/bounded. |
| Repeated capture/open failure cycles | Operation dictionary remains bounded and temporary files do not accumulate. |
| Cancel while opening vs native capture active | Opening cancels and disposes; native capture returns an accurate unsupported-cancel status without claiming it was cancelled. |
| Export overwrite fails during replacement | Previous good profile/snapshot remains recoverable. |
| Domain reload after baseline capture | Session is invalidated predictably; saved baseline remains usable in a new session after reload. |
| Unity object moved/renamed/reimported | Comparison reports identity method and confidence; ambiguous name matches do not appear as exact. |
| Path-to-root traversal hits a budget | Returns truncation metadata and never presents the result as conclusive absence of a root. |
| Missing allocation-site/native-stack support | Domain is unavailable/not captured, not silently represented as zero. |
| Clean checkout without external `.snap` fixture | Snapshot-dependent test state is visible as skipped; CI requires or generates a documented safe fixture for those tests. |
| Connected Player vs Editor capture | Reported metadata/file location matches the actual captured target and the output file is verified. |
| Snapshot/profile disposal | All readers, native snapshot objects and operation records are released or evicted according to bounded policy. |
| Caller passes extreme `maxSteps` | Step count is validated/clamped before main-thread iteration; one tool call cannot bypass the incremental-work budget. |
| Cancelled open is polled afterward | Polling reports the same terminal `Cancelled` receipt within the documented retention window; it is not remapped to `Failed/not found`. |
| Explicit capture/export path leaves project root | Write is rejected or follows an explicit external-path policy; no implicit arbitrary write/overwrite occurs. |

## Source references

**Target**
- [`MemorySnapshotOps.cs`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MemorySnapshotOps.cs)
- [`manage_profiler.py`](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/services/tools/manage_profiler.py)

**Candidate**
- [README / claimed test matrix](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/README.md)
- [`SnapshotSessionManager.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotSessionManager.cs)
- [`SnapshotCaptureManager.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotCaptureManager.cs)
- [`SnapshotQueryEngine.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotQueryEngine.cs)
- [`SnapshotDiffEngine.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/SnapshotDiffEngine.cs)
- [`MemoryProfilerExportEngine.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/MemoryProfilerExportEngine.cs)
- [`PathsToRootsEngine.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Bridge/PathsToRootsEngine.cs)
- [`QueryEngineTests.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Tests/Editor/QueryEngineTests.cs)
- [`SnapshotLifecycleTests.cs`](https://github.com/sabriboughanmi/Unity-MCP-MemoryProfiler/blob/main/Unity-Package/Packages/com.sabriboughanmi.unity.mcp.memory-profiler/Tests/Editor/SnapshotLifecycleTests.cs)

**Official Unity documentation**
- [MemoryProfiler API](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/profiling/memory/memoryprofiler)
- [MemoryProfiler.TakeSnapshot](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/profiling/memory/memoryprofiler/takesnapshot)
- [Memory Profiler snapshot concepts](https://docs.unity.cn/Packages/com.unity.memoryprofiler@1.1/manual/snapshots-concepts.html)
