---
id: tool-groups
slug: /guides/tool-groups
title: Tool Groups and manage_tools
sidebar_label: Tool Groups
description: Per-session visibility for the 50 tools. Activate vfx, animation, ui, testing, etc. only when you need them.
---

# Tool Groups

MCP for Unity groups tools so the model gets the full Core tool schemas immediately while specialized tools remain optional. The server instructions include a compact catalog generated from registered group and tool metadata; it lists group purposes and short tool summaries without duplicating full schemas.

## The groups

| Group | Default | Description |
|---|---|---|
| `core` | enabled | Essential scene, script, asset, and editor tools — always on. |
| `animation` | off | Animator control, AnimationClip creation, 2D sprite-sheet animation. |
| `ui` | off | UI Toolkit — UXML, USS, UIDocument. |
| `vfx` | off | VFX Graph, shaders, procedural textures. |
| `scripting_ext` | off | ScriptableObject management. |
| `testing` | off | Test runner and async test jobs. |
| `probuilder` | off | ProBuilder 3D modeling. Requires `com.unity.probuilder` package. |
| `profiling` | off | Profiler session control, counters, memory snapshots, Frame Debugger. |
| `docs` | off | Unity API reflection and documentation lookup. |
| `asset_gen` | off | AI asset generation (3D model, image, audio; bring your own key) and the Blender Bridge. |

## Enabling groups

Plan the complete task first, then activate all likely-needed optional groups in one call. Avoid activating every group by default.

For one group:

```
manage_tools(action="activate", group="vfx")
```

For several groups:

```
manage_tools(action="activate", groups=["ui", "testing", "docs"])
```

The request validates all names before making changes and applies visibility in one rule. Groups already covered by an explicit session rule in the requested state are skipped. With no explicit session rule, the server applies one so the request can override server-level Editor visibility. Repeating the same request then does not add another visibility rule. FastMCP sends a tool-list-change notification when visibility actually changes; the client must refresh its tool list to expose the newly enabled tools.

## Listing what's available

```
manage_tools(action="list_groups")
```

Returns every group with its current activation state and tool names, plus the generated optional-group catalog.

## Deactivating

```
manage_tools(action="deactivate", group="vfx")
```

Useful when a group's tools are confusing the assistant — e.g., `manage_shader` and `manage_material` both apply to materials in different ways. Disabling the one you're not using keeps the assistant focused.

## Other actions

- `sync` — refreshes visibility from the Unity Editor's per-tool toggle UI. Use after toggling tools in `Window > MCP for Unity > Tools`.
- `reset` — restores defaults (only `core` enabled).

The connection instructions already include the optional-group catalog. Use `list_groups` when activation state or complete metadata is needed; do not call it repeatedly just to rediscover names.

## Why this exists

Three reasons:

1. **Prompt economy**: each visible tool adds tokens to every assistant call. Hiding what you're not using is real money saved at scale.
2. **Routing clarity**: when the LLM picks between 50 tools versus the 30 core tools, the wrong-tool rate drops measurably.
3. **Package hygiene**: tools in `probuilder` only work if `com.unity.probuilder` is installed; hiding them by default avoids confusing errors.

## Server vs. session state

- The Unity Editor maintains a per-tool **toggle UI** (`Window > MCP for Unity > Tools`) that controls server-side visibility.
- The `manage_tools` meta-tool controls **per-session** visibility — different MCP sessions can see different groups even against the same server.

`sync` reconciles the two: it pulls the Editor's toggle states into the current session.

## Related reference

- [`manage_tools`](/reference/tools/core/manage_tools) — full tool reference
- [`tool_groups` resource](/reference/resources) — discoverable group catalog

## Schema and context-size optimization

- Long tool and parameter descriptions are compacted in `tools/list`; registered server definitions are not modified.
- Use `get_tool_details(name="manage_scene")` to retrieve an unabridged definition only when needed.
- Set `UNITY_MCP_TOOL_SCHEMA_COMPACTION=0` to disable description compaction.
- Set `UNITY_MCP_DEREFERENCE_SCHEMAS=false` to keep JSON Schema `$ref` references instead of inlining repeated definitions. Use only with clients that support `$ref`; the default preserves broad compatibility.
- Run `cd Server` then `uv run python src/benchmark_tool_schema_optimization.py` to compare serialized tool-list sizes. Token counts use the approximate four-characters-per-token estimate.
