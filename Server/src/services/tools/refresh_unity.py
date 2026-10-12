from __future__ import annotations

import asyncio
import logging
import os
import time
from collections.abc import Awaitable, Callable
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from models import MCPResponse
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
import transport.unity_transport as unity_transport
import transport.legacy.unity_connection as _legacy_conn
from transport.legacy.unity_connection import _extract_response_reason
from services.state.external_changes_scanner import external_changes_scanner
import services.resources.editor_state as editor_state

logger = logging.getLogger(__name__)

# Blocking reasons that indicate Unity is actually busy (not just stale status).
# Must match activityPhase values from EditorStateCache.cs
_REAL_BLOCKING_REASONS = {"compiling", "domain_reload", "running_tests", "asset_import"}

# States that never clear without someone acting on them, so polling through the timeout only
# delays an answer the caller could already act on.
_WAITING_WONT_HELP_REASONS = {"modal_dialog"}


def _in_pytest() -> bool:
    """Return True when running inside pytest to avoid polling unmocked resources."""
    return "PYTEST_CURRENT_TEST" in os.environ


async def _probe_blocking_modal(
    ctx: Context, *, require_detection: bool = False
) -> dict[str, Any] | None:
    """Probe liveness off the main-thread queue while readiness is stalled.

    If readiness would otherwise be accepted only because no known blocker was reported,
    unsupported modal detection cannot safely provide the missing evidence.
    """
    try:
        unity_instance = await get_unity_instance_from_context(ctx)
        response = await unity_transport.send_with_unity_instance(
            _legacy_conn.async_send_command_with_retry,
            unity_instance,
            "liveness",
            {},
            retry_on_reload=False,
        )
    except Exception:
        return None

    if hasattr(response, "model_dump"):
        response = response.model_dump()
    if not isinstance(response, dict):
        return None

    data = response.get("data") if isinstance(response.get("data"), dict) else {}
    # Preserve a modal classification already produced by PluginHub.
    if response.get("success") is False and data.get("reason") == "modal_dialog":
        return response
    if response.get("success") is not True:
        return None

    modal = data.get("modal") if isinstance(data.get("modal"), dict) else {}
    if not modal.get("supported") and require_detection:
        return MCPResponse(
            success=False,
            error=(
                "The Editor state is not ready and this platform cannot identify modal dialogs. "
                "Inspect the Unity Editor manually before retrying."
            ),
            hint="user_action_required",
            data={
                "reason": "modal_detection_unsupported",
                "main_thread_stall_ms": data.get("main_thread_stall_ms"),
            },
        ).model_dump()

    sample_age_ms = data.get("sample_age_ms")
    if (
        modal.get("supported")
        and isinstance(sample_age_ms, (int, float))
        and sample_age_ms > 5000
    ):
        return MCPResponse(
            success=False,
            error=(
                f"Unity's modal liveness sampler is stale ({sample_age_ms} ms); "
                "readiness cannot be confirmed safely."
            ),
            hint="retry",
            data={"reason": "liveness_probe_stale", "sample_age_ms": sample_age_ms},
        ).model_dump()

    if not modal.get("blocked"):
        return None

    supported = bool(modal.get("supported"))
    answerable = supported and bool(modal.get("answerable"))
    title = modal.get("title") or "(untitled dialog)"
    body = modal.get("body")
    buttons = modal.get("buttons") or []
    kind = modal.get("kind")
    modal_label = (
        "dialog" if kind == "dialog" else
        "window" if kind == "editor_window" else
        "unidentified modal UI"
    )
    detail = f"Unity Editor is blocked by {modal_label} {title!r}"
    if body:
        detail += f": {body}"
    if buttons:
        detail += f" Options: {', '.join(buttons)}."
    if not answerable:
        detail += " Dismiss it in the Editor."

    return MCPResponse(
        success=False,
        error=detail,
        hint="answer_dialog" if answerable else "user_action_required",
        data={
            "reason": "modal_dialog",
            "main_thread_stall_ms": data.get("main_thread_stall_ms"),
            "dialog": {
                "title": title, "body": body, "buttons": buttons,
                "supported": supported, "answerable": answerable,
            },
        },
    ).model_dump()


async def wait_for_editor_ready(
    ctx: Context, timeout_s: float = 30.0
) -> tuple[bool, float, dict[str, Any] | None]:
    """Poll Editor readiness, checking the independent liveness channel before declaring ready.

    Returns (ready, elapsed_seconds, blocked_response). A modal needs an answer, not repeated
    retries; ordinary compilation/import work can clear by itself and should continue to be polled.
    """
    if _in_pytest():
        return (True, 0.0, None)

    start = time.monotonic()
    last_liveness_probe = start - 1.0
    while time.monotonic() - start < timeout_s:
        should_probe_liveness = False
        ready_if_no_modal = False

        try:
            state_resp = await editor_state.get_editor_state(ctx)
            state = state_resp.model_dump() if hasattr(state_resp, "model_dump") else state_resp

            if isinstance(state, dict) and state.get("success") is False:
                reason = (state.get("data") or {}).get("reason")
                if reason in _WAITING_WONT_HELP_REASONS:
                    return (False, time.monotonic() - start, state)
                should_probe_liveness = True

            data = (state or {}).get("data") if isinstance(state, dict) else None
            advice = (data or {}).get("advice") if isinstance(data, dict) else None
            if isinstance(advice, dict):
                if advice.get("ready_for_tools") is True:
                    return (True, time.monotonic() - start, None)

                blocking = set(advice.get("blocking_reasons") or [])
                should_probe_liveness = True
                # Preserve the prior behavior for unknown, non-blocking status reasons, but only
                # after checking the independent transport path for a modal that the state tool
                # itself cannot query while the main thread is stalled.
                ready_if_no_modal = not bool(blocking & _REAL_BLOCKING_REASONS)
        except Exception:
            # Connection errors can be a consequence of the main thread no longer pumping.
            should_probe_liveness = True

        now = time.monotonic()
        if should_probe_liveness and now - last_liveness_probe >= 1.0:
            last_liveness_probe = now
            blocked = await _probe_blocking_modal(ctx, require_detection=ready_if_no_modal)
            if blocked is not None:
                return (False, time.monotonic() - start, blocked)
            if ready_if_no_modal:
                return (True, time.monotonic() - start, None)

        await asyncio.sleep(0.25)

    return (False, time.monotonic() - start, None)


def is_reloading_rejection(resp: Any) -> bool:
    """True when Unity rejected a command because it thinks it is reloading.

    The command was never executed, so retrying is safe.
    """
    if not isinstance(resp, dict) or resp.get("success"):
        return False
    data = resp.get("data") or {}
    return data.get("reason") == "reloading" and resp.get("hint") == "retry"


def is_connection_lost_after_send(resp: Any) -> bool:
    """True when a mutation's response indicates TCP was lost after command was sent.

    Script mutations trigger domain reload which kills the TCP connection.
    The mutation was likely executed but the response was lost.
    """
    if isinstance(resp, dict):
        if resp.get("success"):
            return False
        err = (resp.get("error") or resp.get("message") or "").lower()
    else:
        if getattr(resp, "success", None):
            return False
        err = (getattr(resp, "error", "") or "").lower()
    return "connection closed" in err or "disconnected" in err or "aborted" in err


async def send_mutation(
    ctx: Context,
    unity_instance: str | None,
    command: str,
    params: dict[str, Any],
    *,
    verify_after_disconnect: Callable[[], Awaitable[dict | None]] | None = None,
) -> dict | Any:
    """Send a non-idempotent mutation with reload recovery.

    Handles the full retry/recovery pattern for script mutations:
    1. Send with retry_on_reload=False (don't re-send if Unity is reloading)
    2. If reloading rejection (command never executed) → wait + retry once
    3. If connection lost after send → wait + verify via callback
    4. Wait for editor readiness before returning

    Args:
        verify_after_disconnect: async callable returning a replacement response
            dict if the mutation was verified after connection loss, or None to
            keep the original error response.
    """
    resp = await unity_transport.send_with_unity_instance(
        _legacy_conn.async_send_command_with_retry,
        unity_instance,
        command,
        params,
        retry_on_reload=False,
    )
    if is_reloading_rejection(resp):
        await wait_for_editor_ready(ctx)
        resp = await unity_transport.send_with_unity_instance(
            _legacy_conn.async_send_command_with_retry,
            unity_instance,
            command,
            params,
            retry_on_reload=False,
        )
    if is_connection_lost_after_send(resp) and verify_after_disconnect:
        await wait_for_editor_ready(ctx)
        verified = await verify_after_disconnect()
        if verified is not None:
            resp = verified
    await wait_for_editor_ready(ctx)
    return resp


async def verify_edit_by_sha(
    unity_instance: str | None,
    name: str,
    path: str,
    pre_sha: str | None,
) -> bool:
    """Verify a script edit was applied by comparing SHA before and after.

    Returns True if the file's SHA changed (edit likely applied).
    """
    if not pre_sha:
        return False
    try:
        verify = await unity_transport.send_with_unity_instance(
            _legacy_conn.async_send_command_with_retry,
            unity_instance,
            "manage_script",
            {"action": "get_sha", "name": name, "path": path},
        )
        if isinstance(verify, dict) and verify.get("success"):
            new_sha = (verify.get("data") or {}).get("sha256")
            return bool(new_sha and new_sha != pre_sha)
    except Exception as exc:
        logger.debug(
            "Failed to verify edit after disconnect for %s at %s: %r",
            name, path, exc,
        )
    return False


@mcp_for_unity_tool(
    description="Request a Unity asset database refresh and optionally a script compilation. Can optionally wait for readiness.",
    annotations=ToolAnnotations(
        title="Refresh Unity",
        destructiveHint=True,
    ),
)
async def refresh_unity(
    ctx: Context,
    mode: Annotated[Literal["if_dirty", "force"], "Refresh mode"] = "if_dirty",
    scope: Annotated[Literal["assets", "scripts", "all"],
                     "Refresh scope"] = "all",
    compile: Annotated[Literal["none", "request"],
                       "Whether to request compilation"] = "none",
    wait_for_ready: Annotated[bool,
                              "If true, wait until mcpforunity://editor/state reports data.advice.ready_for_tools true"] = True,
    on_external_scene_change: Annotated[Literal["auto", "reload", "keep_editor"],
                                        "When an open scene changed on disk: auto reloads only if nothing is lost, reload takes the disk version, keep_editor overwrites it"] = "auto",
) -> MCPResponse | dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params: dict[str, Any] = {
        "mode": mode,
        "scope": scope,
        "compile": compile,
        "wait_for_ready": bool(wait_for_ready),
        "on_external_scene_change": on_external_scene_change,
    }

    recovered_from_disconnect = False
    # Don't retry on reload - refresh_unity triggers compilation/reload,
    # so retrying would cause multiple reloads (issue #577)
    response = await unity_transport.send_with_unity_instance(
        _legacy_conn.async_send_command_with_retry,
        unity_instance,
        "refresh_unity",
        params,
        retry_on_reload=False,
    )

    # Handle connection errors during refresh/compile gracefully.
    # Unity disconnects during domain reload, which is expected behavior - not a failure.
    # If we sent the command and connection closed, the refresh was likely triggered successfully.
    # Convert MCPResponse to dict if needed
    response_dict = response if isinstance(response, dict) else (response.model_dump() if hasattr(response, "model_dump") else response.__dict__)
    if not response_dict.get("success", True):
        hint = response_dict.get("hint")
        err = (response_dict.get("error") or response_dict.get("message") or "").lower()
        reason = _extract_response_reason(response_dict)

        # Connection closed/timeout during compile = refresh was triggered, Unity is reloading
        # This is SUCCESS, not failure - don't return error to prevent Claude Code from retrying
        is_connection_lost = (
            "connection closed" in err
            or "disconnected" in err
            or "aborted" in err  # WinError 10053: connection aborted
            or "timeout" in err
            or reason == "reloading"
        )

        if is_connection_lost and compile == "request":
            # EXPECTED BEHAVIOR: When compile="request", Unity triggers domain reload which
            # causes connection to close mid-command. This is NOT a failure - the refresh
            # was successfully triggered. Treating this as success prevents Claude Code from
            # retrying unnecessarily (which would cause multiple domain reloads - issue #577).
            # The subsequent wait_for_ready loop (below) will verify Unity becomes ready.
            logger.info("refresh_unity: Connection lost during compile (expected - domain reload triggered)")
            recovered_from_disconnect = True
        elif hint == "retry" or "could not connect" in err:
            # Retryable error - proceed to wait loop if wait_for_ready
            if not wait_for_ready:
                return MCPResponse(**response_dict)
            recovered_from_disconnect = True
        else:
            # Non-recoverable error - connection issue unrelated to domain reload
            logger.warning(f"refresh_unity: Non-recoverable error (compile={compile}): {err[:100]}")
            return MCPResponse(**response_dict)

    # Optional server-side wait loop (defensive): if Unity tool doesn't wait or returns quickly,
    # poll the canonical editor_state resource until ready or timeout.
    ready_confirmed = False
    if wait_for_ready:
        ready_confirmed, waited_s, blocked = await wait_for_editor_ready(ctx, timeout_s=60.0)

        # A blocking state that waiting cannot clear (a modal dialog) is reported as itself, with
        # whatever action clears it, rather than as an anonymous 60s timeout.
        if blocked is not None:
            logger.warning(
                "refresh_unity: editor blocked after %.1fs: %s", waited_s, blocked.get("error"))
            return MCPResponse(**blocked)

        # If we timed out without confirming readiness, log and return failure
        if not ready_confirmed:
            logger.warning("refresh_unity: Timed out after 60s waiting for editor to become ready")
            return MCPResponse(
                success=False,
                message="Refresh triggered but timed out after 60s waiting for editor readiness.",
                data={"timeout": True, "wait_seconds": 60.0},
            )

    # After readiness is restored, clear any external-dirty flag for this instance so future tools can proceed cleanly.
    try:
        inst = unity_instance or await editor_state.infer_single_instance_id(ctx)
        if inst:
            external_changes_scanner.clear_dirty(inst)
    except Exception:
        pass

    if recovered_from_disconnect:
        return MCPResponse(
            success=True,
            message="Refresh recovered after Unity disconnect/retry; editor is ready.",
            data={"recovered_from_disconnect": True},
        )

    return MCPResponse(**response_dict) if isinstance(response, dict) else response
