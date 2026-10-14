# Script-First Deterministic Scene Workflow

## Contract

The persistent, version-controlled scene generator is the source of truth for generated scene state. To change the output, update the generator and rerun it. Do not repair the generated result using a sequence of ad hoc GameObject/component mutations that are absent from the source.

A generator may call Unity APIs to create objects, assign components, configure assets and save a scene; the constraint is that this logic must live in repeatable project source.

## Recommended run sequence

1. **Declare ownership.** Identify output scene path and whether the generator owns the entire scene or only a named generated root/region. Protect unrelated hand-authored content.
2. **Edit persistent source.** Use script helpers with a source-version/hash precondition. Store supporting configuration/assets in stable project paths.
3. **Validate and compile.** Run source validation, wait through compile/domain reload, and obtain the actual compiler result. Readiness alone is not compile success. A compile failure blocks execution.
4. **Resolve Editor identity.** Confirm the target project and Editor instance before invoking the generator.
5. **Invoke once with a run ID.** Use a named Editor entry point, typically an Editor menu item. A lost response should be reconciled by ID/status/receipt, not by blindly repeating a potentially destructive run.
6. **Regenerate inside an explicit boundary.** Replace only generator-owned content; define staging/save/recovery behavior. Do not infer ownership from object names alone if a marker/manifest can be used.
7. **Run generator-owned assertions.** Return a compact report: scene path; generator version/hash; run ID; status; counts; missing scripts/references/assets; duplicate generated IDs; selected component/property checks; warnings; save status; elapsed time.
8. **Save only on success.** Keep the last-known-good output recoverable. Failed assertions should name expected/actual values and relevant generator sections, not silently patch the result.
9. **Inspect visuals selectively.** First inspect one truthful Game View capture when possible; use Scene View/camera/surround/orbit only when needed. Check capture-source and fallback metadata. On failure, fix the generator and regenerate.
10. **Verify repeatability.** Run twice with identical inputs and compare semantic output. Byte-identical scene YAML is a separate requirement and should not be assumed.

## Determinism requirements

- **Inputs:** explicit scene path, generator revision, config/data assets, relevant flags, and random seed when randomness is intended.
- **Ordering:** sort discovered asset/reference inputs where APIs do not promise stable order. Do not depend on selection, open-scene order, unsaved state or arbitrary hierarchy enumeration.
- **Identity:** explicit generated-root/section ownership; stable generated IDs where required.
- **References:** use GUIDs or validated project-relative paths where appropriate. Report missing/ambiguous assets; never silently substitute another.
- **Save boundary:** define open/additive scene behavior and exactly when a scene is saved.
- **Idempotence:** repeated runs converge; no accumulated duplicate objects/components/materials or stale generated data.
- **Output:** bounded machine-readable receipt and human-readable summary; detailed logs remain opt-in.

## Current MCP tools and boundaries

The reviewed baseline already has script create/edit/validation helpers, scene open/create/save operations, named menu execution, and screenshot/diagnostic tools. Use them as pipeline building blocks; do not duplicate them before gap analysis.

| Existing tool area | Appropriate use | Boundary |
|---|---|---|
| Script creation/structured edits | Persistent generator source | Avoid ephemeral code as the only copy; validate source preconditions |
| Script validation / refresh | Preflight and compile/reload coordination | Actual compiler errors/success must be captured separately |
| Menu-item execution | Named generator entry point | Gate on successful compilation and correct Editor/project identity |
| Scene management | Open/create/save and targeted scene state | No ad hoc corrections outside the generator |
| Execute Code | Small trusted diagnostics/bootstrap only | Not a durable generator and not a sandbox |
| Object/component queries | Targeted read-only assertions | Do not dump the whole scene to decide what to fix |
| Screenshot / profiler tools | Verify specific visual/rendering hypotheses | Respect capture and response budgets |

## Missing lifecycle signal

Add a request-correlated compile/result contract:
- Correlate compile start, per-assembly diagnostics and compile finish to one cycle.
- Use `UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished` for structured `CompilerMessage` data, and `compilationFinished` to finalize the cycle; verify exact Unity-version behavior in tests.
- Persist the compact last-cycle summary across reload with a suitable Editor session mechanism.
- Store a generator receipt keyed by run ID with source hash/version, target scene, timestamps, completion state, assertions and save result.
- Differentiate “request accepted,” “Editor ready,” “compilation succeeded,” “generator finished,” and “validation passed.”

See the archived [generator lifecycle review](../Research-Backup/DETERMINISTIC_GENERATOR_LIFECYCLE_REVIEW.md) and [scene authoring notes](../Research-Backup/DETERMINISTIC_SCENE_AUTHORING.md) for source details.
