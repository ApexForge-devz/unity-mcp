"""
manage_tools - server-only meta-tool for dynamic tool group activation.

This tool lets the AI assistant (or user) discover available tool groups
and selectively enable / disable them for the current session. Activating
a group makes its tools appear in tool listings; deactivating hides them.

Works on all transports (stdio, HTTP, SSE) via FastMCP 3.x native
per-session visibility.
"""
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import (
    mcp_for_unity_tool,
    TOOL_GROUPS,
    DEFAULT_ENABLED_GROUPS,
    get_group_tool_names,
)


@mcp_for_unity_tool(
    unity_target=None,
    group=None,
    description=(
        "List or manage session-visible tool groups. Plan the task first, then use "
        "groups=[...] to activate or deactivate several optional groups in one call. "
        "Core is available directly. Use sync only after changing the Unity Editor tool toggles."
    ),
    annotations=ToolAnnotations(
        title="Manage Tools",
        readOnlyHint=False,
        destructiveHint=False,
        idempotentHint=True,
        openWorldHint=False,
    ),
)
async def manage_tools(
    ctx: Context,
    action: Annotated[
        Literal["list_groups", "activate", "deactivate", "sync", "reset"],
        "Action to perform."
    ],
    group: Annotated[
        str | None,
        "One group for activate/deactivate. Use groups for batches."
    ] = None,
    groups: Annotated[
        list[str] | None,
        "Multiple group names to activate/deactivate in one call. Use either group or groups, not both."
    ] = None,
) -> dict[str, Any]:
    if action == "list_groups":
        return await _list_groups(ctx)

    if action in ("activate", "deactivate"):
        if group is not None and groups is not None:
            return {"error": "Use either 'group' or 'groups', not both."}

        requested = groups if groups is not None else ([group] if group is not None else [])
        if not requested:
            return {"error": f"group or groups is required for {action}"}

        normalized: list[str] = []
        for value in requested:
            if not isinstance(value, str) or not value.strip():
                return {"error": "Group names must be non-empty strings."}
            name = value.strip().lower()
            if name not in normalized:
                normalized.append(name)

        unknown = [name for name in normalized if name not in TOOL_GROUPS]
        if unknown:
            return {
                "error": f"Unknown group(s): {', '.join(unknown)}. "
                f"Valid groups: {', '.join(sorted(TOOL_GROUPS))}. No changes were made."
            }

        desired_enabled = action == "activate"
        # Only an explicit session rule proves a group is already in the requested
        # state. Server-level Editor toggles can expose groups without a session rule.
        session_states = await _get_explicit_session_group_states(ctx)
        changed = [
            name for name in normalized
            if session_states.get(name) != desired_enabled
        ]
        unchanged = [name for name in normalized if name not in changed]

        # One visibility rule for the complete requested set yields one list-change notification.
        if changed:
            action_verb = "Activating" if desired_enabled else "Deactivating"
            await ctx.info(f"{action_verb} tool groups: {', '.join(changed)}")
            tags = {f"group:{name}" for name in changed}
            if desired_enabled:
                await ctx.enable_components(tags=tags, components={"tool"})
            else:
                await ctx.disable_components(tags=tags, components={"tool"})

        group_tools = get_group_tool_names()
        is_single = groups is None and group is not None
        verb = "activated" if desired_enabled else "deactivated"
        state_word = "visible" if desired_enabled else "hidden"
        if is_single:
            name = normalized[0]
            message = (
                f"Group '{name}' is now {state_word}."
                if name in changed else
                f"Group '{name}' was already {state_word}; no visibility change was needed."
            )
            return {
                verb: name,
                "tools": group_tools.get(name, []),
                "changed": name in changed,
                "message": message,
            }

        group_key = "activated_groups" if desired_enabled else "deactivated_groups"
        return {
            group_key: normalized,
            "changed_groups": changed,
            "unchanged_groups": unchanged,
            "tools_by_group": {name: group_tools.get(name, []) for name in normalized},
            "message": (
                f"{len(changed)} group(s) changed; {len(unchanged)} were already "
                f"{state_word}. The requested groups are now {state_word}."
            ),
        }

    if action == "sync":
        await ctx.info("Syncing tool visibility from Unity Editor...")
        from services.tools import sync_tool_visibility_from_unity
        result = await sync_tool_visibility_from_unity(notify=True)
        if result.get("error"):
            msg = result["error"]
            if result.get("unsupported"):
                msg = (
                    "The connected Unity Editor does not support tool state syncing yet. "
                    "Update the MCPForUnity package to the latest version, then try again. "
                    "In the meantime, use activate/deactivate actions to toggle groups manually."
                )
            else:
                msg = f"Failed to sync tool visibility from Unity. Is Unity running? ({msg})"
            return {"error": msg}
        return {
            "synced": True,
            "enabled_groups": result.get("enabled_groups", []),
            "disabled_groups": result.get("disabled_groups", []),
            "enabled_tool_count": result.get("enabled_tool_count", 0),
            "total_tool_count": result.get("total_tool_count", 0),
            "message": (
                "Tool visibility synced from Unity Editor. "
                f"Enabled groups: {', '.join(result.get('enabled_groups', []))}. "
                f"Disabled groups: {', '.join(result.get('disabled_groups', []) or ['none'])}."
            ),
        }

    if action == "reset":
        await ctx.info("Resetting tool visibility to defaults")
        await ctx.reset_visibility()
        return {
            "reset": True,
            "default_groups": sorted(DEFAULT_ENABLED_GROUPS),
            "message": "Tool visibility restored to server defaults.",
        }

    return {"error": f"Unknown action '{action}'"}


async def _get_explicit_session_group_states(ctx: Context) -> dict[str, bool]:
    """Return only explicit session visibility rules, with the latest rule winning."""
    session_enabled: dict[str, bool] = {}
    try:
        rules = await ctx._get_visibility_rules()
        for rule in rules:
            tags = rule.get("tags") or []
            enabled = rule.get("enabled", True)
            for tag in tags:
                if isinstance(tag, str) and tag.startswith("group:"):
                    session_enabled[tag[len("group:"):]] = enabled
    except Exception:
        pass  # Unknown state means mutations should fail toward applying requested visibility.
    return session_enabled


async def _get_session_group_states(ctx: Context) -> dict[str, bool]:
    """Return effective session group states, falling back to server defaults."""
    explicit = await _get_explicit_session_group_states(ctx)
    return {
        name: explicit.get(name, name in DEFAULT_ENABLED_GROUPS)
        for name in TOOL_GROUPS
    }


async def _list_groups(ctx: Context) -> dict[str, Any]:
    """Build the list_groups response with group metadata and tool names."""
    group_tools = get_group_tool_names()
    session_enabled = await _get_session_group_states(ctx)

    groups = []
    for name in sorted(TOOL_GROUPS.keys()):
        currently_enabled = session_enabled[name]
        groups.append({
            "name": name,
            "description": TOOL_GROUPS[name],
            "enabled": currently_enabled,
            "default_enabled": name in DEFAULT_ENABLED_GROUPS,
            "tools": group_tools.get(name, []),
            "tool_count": len(group_tools.get(name, [])),
        })
    return {
        "groups": groups,
        "note": (
            "Plan the task and activate all likely-needed optional groups together with "
            "manage_tools(action='activate', groups=[...]). Core is already available. "
            "Tools with group=None (server meta-tools) are always visible."
        ),
    }
