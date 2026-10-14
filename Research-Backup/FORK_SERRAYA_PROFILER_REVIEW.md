# Direct Fork Review — Serraya Frame Debugger and Recorded Profiler Analysis

**Target fork:** [Serraya/unity-mcp](https://github.com/Serraya/unity-mcp), branch beta  
**Relationship:** Direct fork of CoplayDev/unity-mcp  
**Compare snapshot:** 1,574 ahead, 0 behind and 110 changed files reported by GitHub compare; its branch includes a merge from current upstream beta. The high ahead count reflects divergent/specialized history rather than 1,574 independent fixes.  
**Reviewed:** 2026-10-14  
**Scope:** Frame Debugger data extraction, render-target output, marker calltree, recorded-frame profiler analysis and selected tests. Source review only; no Unity Editor tests were run here.

## 1. What the fork adds

Compared with target beta's FrameDebuggerOps.cs (about 11 KB, three actions: enable, disable, paginated get_events), the fork's FrameDebuggerOps.cs is about 65 KB and the same profiler dispatcher adds:
- frame_debugger_get_event_details;
- frame_debugger_capture_event_output;
- get_marker_calltree;
- get_frame_summary;
- get_hot_markers;
- find_marker;
- export_profile_tables;
- profiler_job_status and profiler_job_cancel.

Key source files:
- [FrameDebuggerOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/FrameDebuggerOps.cs), blob a7f40556164db7f57a574e6be3e307cf649718fd.
- [MarkerCalltreeOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MarkerCalltreeOps.cs), blob 69ba2b1827dc71b27c2c18bddc2890324fd73d53.
- [RecordedFrameAnalysisOps.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/RecordedFrameAnalysisOps.cs), blob 0776da6438099940a9a8e4890ad916e5ac0ded53.
- [MarkerFilterRegex.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/Operations/MarkerFilterRegex.cs), blob 973941d6ef6901fbca08586dae6e002af1c02b0e.
- [ManageProfiler.cs](https://github.com/Serraya/unity-mcp/blob/beta/MCPForUnity/Editor/Tools/Profiler/ManageProfiler.cs), blob 8ea6e3f2b28e9667243bc67fb63ca6832c3d84b7.
- [ProfilerJobPumpTests.cs](https://github.com/Serraya/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ProfilerJobPumpTests.cs), blob c7e855ed9e05e88b369f208c9fd7ccb4bd24e32b.
- [ProfilerMarkerRegexTests.cs](https://github.com/Serraya/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ProfilerMarkerRegexTests.cs), blob ceb621f67f83169338eefa75333a1168d6a9a305.

## 2. Frame Debugger event extraction: improvements worth adapting

### 2.1 Explicit field-level completeness

The fork reads Unity Editor internals via reflection and checks multiple member aliases. It uses bounded diagnostic payloads for missing fields and warnings, then returns them in the response rather than silently dropping every failed per-event read. This addresses a target limitation: target get_events can skip an event when event-data retrieval fails, leaving the client without a clear indication of how much detail is missing.

The expanded reader adds:
- paginated event rows (default 50, max 500);
- separate full event detail queries with selected shader properties;
- aliases for Unity-version differences in event descriptors, draw-call data, shaders, render targets, pipeline state, batching, resource lists and object references;
- bounded generic object fields, shader properties, warning count and missing-field diagnostics;
- an explicit error when event data is missing/unavailable, rather than treating failed extraction as a normal empty record.

The structure is a useful pattern: keep the small listing compact, fetch an event's detailed state on demand, expose what is incomplete and preserve stable error codes. The cost is a large amount of Unity-internal reflection and alias logic; port only after tests establish which Unity editor versions and event shapes the target supports.

### 2.2 Render-target output capture

The new capture action extracts the selected event's RenderTexture, reads pixels, encodes PNG, writes a file and can optionally return base64. It reports structured errors for missing RenderTexture, invalid dimensions, capture/write failures, and includes a sampled non-zero-pixel diagnostic to help detect empty output.

**Two important constraints to fix before adaptation:**
1. It checks only that width/height are greater than zero before allocating a full Texture2D and PNG byte array. A very large target can cause a large transient allocation or editor memory spike. Add a maximum pixel/byte budget and/or bounded downscale before allocating; compute width × height with overflow-safe arithmetic. Do not rely on output being file-backed to keep memory bounded.
2. A caller-provided output_path is normalized and rejected if it sits under Assets, but otherwise can point to an arbitrary filesystem location; the writer uses File.WriteAllBytes, which overwrites an existing .png. Default output is under a temporary directory, but the optional path broadens write scope. Restrict exports to a defined MCP-owned output root by default, and require an explicit overwrite policy or unique/create-new behavior for existing files.

The current test set did not reveal a dedicated frame-output capture test in this fork tree. Before porting, add fake/reflection-injected tests for path rejection, existing-file behavior, maximum dimensions, output byte budget, cleanup and the previous RenderTexture.active state being restored after all exceptions.

## 3. Marker calltree and recorded-frame analysis

### 3.1 Bounded marker regex

MarkerFilterRegex compiles user-provided marker_filter patterns with a one-second Regex match timeout and returns an actionable structured timeout error instead of allowing catastrophic backtracking to hang the Editor main thread. The added tests verify valid patterns, pathological patterns, invalid syntax and structured timeout results. This is a small, high-quality correctness pattern appropriate for any profiler/trace query accepting regex.

### 3.2 Calltree scan budget

MarkerCalltreeOps reads Unity's HierarchyFrameDataView for a specified recorded frame, scans matching thread(s), returns matching markers with optional parent/child context, and bounds max depth (64), rows (2,000), and scanned thread count (128). A shared row budget ensures parent/child expansion cannot exceed the result cap. It reports searched threads, scan counts, truncation and the available frame range.

### 3.3 Recorded-frame summary, hot markers and CSV export

RecordedFrameAnalysisOps provides frame-time summary, worst-frame summaries, hot marker aggregate statistics, marker search, CSV export and asynchronous jobs for ranges above a threshold. Export responses return file paths, table columns, row/frame bounds and truncation details. The paired profiler job pump test documents and checks a starvation fix: each active job must make bounded progress during each pump instead of letting the first long-running job consume the entire shared frame budget.

These are valuable for Unity performance work because they convert a recorded profiler capture into evidence-oriented queries instead of sending the raw Profiler hierarchy. The output is deliberately bounded, and expensive analysis is optionally moved into a pollable job. However, the code currently has only two newly identified specialist EditMode test files in the fork tree for the frame-debugger/recorded-profiler changes, so its broad data extraction compatibility is not comprehensively evidenced by its tests.

## 4. Adoption risks and tests required

1. **Internal API version drift:** FrameDebuggerUtility and event-data fields are private/internal and move across Unity versions. A reflection member found by name is not proof the semantics match. Build a version matrix with actual Frame Debugger captures and report unsupported fields.
2. **Correct event identity:** a response must be explicit when event data for one index cannot be obtained. Never return cached or adjacent-event payload as if it belonged to the requested index.
3. **Output allocation and filesystem scope:** cap resolution/encoded bytes, default to an MCP-owned export folder, use unique names/CreateNew unless overwrite was explicitly approved, and never write into Assets unless the user explicitly requests an import workflow.
4. **Job pump fairness:** keep per-job bounded step execution and round-robin fairness; cancellation must release active-job registry entries and update hooks. Test multiple jobs with slow steps, cancel, completion, polling and errors.
5. **Response budget:** cap shader properties, nested generic members, warnings, rows and marker scans. Retain truncation and missing-field indicators in outputs.
6. **Semantic statistics:** marker time totals/medians/quartiles are derived from Profiler data; validate aggregation semantics against Unity Profiler UI or a known synthetic profile. Do not claim exact Profiler Analyzer compatibility unless the output is validated.
7. **No live tests executed here:** the source files and regression tests were inspected but not run in Unity.

## 5. Recommendation

**Decision: ADAPT selectively, as a P2 profiling capability expansion.**

Priority order:
1. Add detailed event inspection with field completeness diagnostics and per-event identity.
2. Add bounded render-target capture with restricted output path and pixel/byte cap.
3. Reuse the bounded regex and fair async-job scheduler patterns for recorded frame analysis.
4. Add marker calltree, hot-marker analysis and CSV tables as separate actions only after validating frame/thread/range semantics and output costs.

Do not cherry-pick the 65 KB FrameDebuggerOps.cs as a unit. Preserve target naming, response conventions, target-specific Unity compatibility and test layout; port by contract, with fake/reflection fixtures first and live Editor captures second.
