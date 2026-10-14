# Direct Fork Review — haohao-0407 Meshy Recovery and Model Options

**Target fork:** [haohao-0407/unity-mcp](https://github.com/haohao-0407/unity-mcp), branch **beta**  
**Relationship:** Direct fork of CoplayDev/unity-mcp  
**Compare snapshot:** 7 ahead, 0 behind; 22 changed files.  
**Reviewed:** 2026-10-14  
**Scope:** Meshy generation-state durability, duplicate-submission avoidance, preview/refine recovery, model parameter coverage. Source review only; tests were not executed here.

## 1. Summary

This is a narrow, useful fork contribution rather than a broad rearchitecture. It adds a provider-specific checkpoint for Meshy model jobs and an atomic disk journal in addition to the existing SessionState job snapshot. It also expands Meshy parameter modeling and adds a true domain-reload regression test. The core adoption candidate is **recovery correctness and no duplicate paid generation**, not blind adoption of every API parameter or model identifier.

Target beta persists generic AssetGenJob records only in Unity SessionState. The inspected target manager does not include this fork's Meshy-specific disk journal, confirmed-root checkpoint, preview/refine checkpoint or CanResume logic.

## 2. Checkpoints and journal

[Fork AssetGenJobManager.cs](https://github.com/haohao-0407/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/AssetGenJobManager.cs), blob 7f5109575be6ca2f54476faa3769e46bb57b692f:
- keeps SessionState snapshots for current-domain/session visibility;
- writes Meshy model job snapshots to an atomic JSON journal under a Library-local path (production path is resolved from the job manager; tests override it with a temporary directory);
- records a MeshyCheckpoint with confirmed root/preview task ID, optional refine task ID, mode/format/texture state, and whether a submission is still pending;
- records output folder/name and downloaded path required to finish the existing job instead of creating a new generation;
- exposes CanResume only when provider is Meshy, kind is model, state is not Done, a checkpoint exists, the submission is no longer pending, and a confirmed root task ID is present;
- restores the adapter from the checkpoint and resumes polling the existing remote task after reload;
- does not persist provider API keys in the job/checkpoint object, according to its source contract;
- writes to a .tmp file, then replaces/moves it into place, reducing the chance of leaving a partially overwritten journal record.

Target's current manager, blob bb4b3051515bacb6e203ba13521fdeec213c074b, serializes jobs under the MCPForUnity.AssetGen.Job.* SessionState keys and has generic state persistence, but not this separate disk journal or Meshy checkpoint contract.

### The important no-retry boundary

The fork distinguishes two materially different failure states:

**Known remote task ID:** it has a confirmed provider task ID and saved parameters/checkpoint, so it can resume polling and finish download/import without reissuing the paid generation POST.

**Submission pending / remote ID unknown:** the POST may have reached Meshy even if the local call timed out or the Editor reloaded before receiving the ID. The fork marks the checkpoint as SubmissionPending; the job is not eligible for automatic resume. It fails with an explicit explanation and requires an explicit recovery path rather than blindly posting again. This avoids duplicate charges and duplicate remote work where the provider does not offer an idempotency key.

This is a high-value state-machine invariant and should be ported as a general rule for any billable asynchronous provider. A retry-safe client cannot infer “POST did not happen” solely from a lost response.

## 3. Tests that exercise this boundary

[Fork MeshyRecoveryTests.cs](https://github.com/haohao-0407/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/MeshyRecoveryTests.cs), blob 15a2bfb5174e700842e6d476d6fe7db8d8aa3da4, includes cases for:
- resumed job polling without another paid POST;
- checkpointing a refine task ID and resuming on the refine endpoint;
- a pending/unknown submission becoming failed and explicitly not being resubmitted after simulated reload;
- timeout preserving an already confirmed remote ID and explicit resume completing without another POST;
- durable cancellation and no automatic resume;
- manually attaching to an existing remote task ID in text/image mode and making that attachment idempotent;
- exposing both local job ID and provider task ID/recovery state through status/list actions.

[Fork MeshyDomainReloadTests.cs](https://github.com/haohao-0407/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/MeshyDomainReloadTests.cs), blob 322f39fb1ced9e594a67c7e2d7f7dff286bcbfd8, uses an InitializeOnLoadMethod test bootstrap and EditorUtility.RequestScriptReload to force an actual domain reload, restores the fake transport/checkpoint, verifies the remote task ID survives, completes the task and asserts the paid submission count stays at one.

These source files provide better evidence than mocks that only serialize/deserialze one object, because they test lifecycle across an actual editor-domain boundary. The tests themselves were not run during this research session and still need execution in the supported Unity CI matrix.

## 4. Expanded Meshy model options

[Fork MeshyModelOptions.cs](https://github.com/haohao-0407/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Providers/MeshyModelOptions.cs), blob 04799487be128f12245b48c619d841ad3f878238, models Meshy parameters in a separate typed configuration. It represents geometry resolution, model type/topology, remesh/decimation/polycount, pose, PBR, texture resolution/model/prompt/reference image, lighting removal, moderation, output formats, auto-size/origin, preview task/input task and other provider flags. It validates choices/ranges/combinations before constructing request bodies rather than spreading string conversions through the adapter.

The adapter explicitly separates text-to-3D preview from a following refine task and persists enough information to recover that two-phase flow.

**Adoption caution:** provider models, allowed values, output formats and parameter requirements are API-version-sensitive. The source has explicit checks for supported model identifiers and disables older models in this fork, but those rules must be reconciled against current official Meshy documentation and real endpoint behavior before porting. Do not treat a downstream fork's model catalog as an authoritative provider contract.

The fork expands the generate_model.py wrapper to offer resume and list_jobs actions and describe the new options. This is a useful interface, but recovery actions should only be exposed when the corresponding Unity-side state checks are tested.

## 5. Risks to address before adapting

1. **Crash window during POST:** a journal can prevent duplicate submissions only if it persists a “submission pending” state before the HTTP POST and changes it to “confirmed ID” only after the response is parsed. Verify that ordering in the precise submit code for every generation path.
2. **Atomic file semantics:** File.Replace behavior varies by platform/filesystem; test first write, replacement, temp-file residue, process interruption during write, read-only/locked Library, corrupted JSON and stale journal cleanup on Windows/macOS/Linux.
3. **Journal privacy and trust:** keep provider task IDs and asset path/state, but never API keys, bearer headers, signed download URLs, prompts containing secrets, or raw provider payloads unless an explicit retention design exists. Sanitize exceptions and define journal retention/max-file policy.
4. **Manual attach vs auto-recovery:** manually supplying a remote task ID should never trigger a new generation POST. Ensure mode/endpoint and task owner are explicit enough to prevent polling the wrong task.
5. **Checkpoint monotonicity:** preview/refine state must move forward durably before later requests rely on it. After refine POST success, checkpoint the refine ID before first refine poll. A failed checkpoint write must produce a safe failure, not an implicit new submission.
6. **Deadlines and provider-specific phases:** timeout, cancellation and import failure should preserve enough remote identity for deliberate resume without resubmitting generation. Avoid automatically restarting cancelled jobs or replaying an in-flight POST.
7. **API-contract freshness:** validate model/option constraints from official provider docs and endpoint responses; add schema/catalog drift tests.

## 6. Adoption decision

**Decision: ADAPT (P1 reliability candidate).**

First extract the generic invariants into a provider-job state model:
- separate local request/job ID from provider task ID;
- persist a submission-pending marker before non-idempotent submit;
- persist confirmed task/checkpoint before returning success to the poll loop;
- on restart, resume only confirmed remote work;
- never auto-resubmit when the prior POST's outcome is unknown;
- provide explicit opt-in resume/attach actions and clearly distinguish them from creating a new task;
- preserve task ownership across domain reload, cancellation and Editor restart without persisting credentials.

Then port focused Meshy options only after verifying provider documentation and adding parameter-schema tests. Prefer reusing the existing AssetGenJobManager instead of introducing a separate job framework.
