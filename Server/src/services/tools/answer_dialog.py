from __future__ import annotations

import logging
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from models import MCPResponse
from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
import transport.unity_transport as unity_transport
import transport.legacy.unity_connection as _legacy_conn

logger = logging.getLogger(__name__)

_LIVENESS_SAMPLE_MAX_AGE_MS = 5000


@mcp_for_unity_tool(
    description=(
        "Reads or answers a modal dialog blocking the Unity Editor. A modal blocks every other "
        "tool until answered. Omit button to read the dialog; pass button to press it."
    ),
    annotations=ToolAnnotations(
        title="Answer Unity Dialog",
        destructiveHint=True,
    ),
)
async def answer_dialog(
    ctx: Context,
    button: Annotated[str | None,
                      "Exact button label from the dialog's buttons list. Omit to read without answering."] = None,
    expect_title: Annotated[str | None,
                            "Expected dialog title; the press is refused if a different dialog is open."] = None,
) -> MCPResponse | dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    if button is None:
        # Read-only: the liveness snapshot carries the dialog contents.
        response = await unity_transport.send_with_unity_instance(
            _legacy_conn.async_send_command_with_retry,
            unity_instance,
            "liveness",
            {},
            retry_on_reload=False,
        )
        # A failed probe carries no modal, which would otherwise read as "nothing is blocking" and
        # hide the transport error behind a confident all-clear.
        if isinstance(response, dict) and response.get("success") is False:
            return MCPResponse(**response)

        data = response.get("data") if isinstance(response, dict) else None
        if isinstance(data, dict):
            sample_age_ms = data.get("sample_age_ms")
            if isinstance(sample_age_ms, (int, float)) and sample_age_ms > _LIVENESS_SAMPLE_MAX_AGE_MS:
                return MCPResponse(
                    success=False,
                    error=(
                        f"Unity's modal liveness snapshot is stale ({sample_age_ms} ms); "
                        "refusing to report that no dialog is open."
                    ),
                    hint="retry",
                    data={"reason": "liveness_probe_stale", "sample_age_ms": sample_age_ms},
                )

        modal = (data or {}).get("modal") if isinstance(data, dict) else None

        if not isinstance(modal, dict) or "blocked" not in modal:
            return MCPResponse(
                success=False,
                error="Unity did not return modal-detection state; refusing to report that no dialog is open.",
                hint="retry",
                data={"reason": "liveness_payload_missing"},
            )

        if not modal.get("supported"):
            return MCPResponse(
                success=True,
                message=(
                    "Modal-dialog identification is not supported on this platform. "
                    "The liveness heartbeat is available; inspect the Unity Editor manually if commands are stalled."
                ),
                data={
                    "blocked": None,
                    "supported": False,
                    "main_thread_stall_ms": data.get("main_thread_stall_ms"),
                },
            )

        if not modal.get("blocked"):
            return MCPResponse(
                success=True,
                message="No modal dialog is currently open in the Unity Editor.",
                data={"blocked": False, "supported": True},
            )

        return MCPResponse(
            success=True,
            message=f"Dialog open: {modal.get('title')!r}",
            data={
                "blocked": True,
                "dialog": {
                    "title": modal.get("title"),
                    "body": modal.get("body"),
                    "buttons": modal.get("buttons") or [],
                    "answerable": bool(modal.get("answerable")) and bool(modal.get("supported")),
                },
                "main_thread_stall_ms": (data or {}).get("main_thread_stall_ms"),
            },
        )

    params: dict[str, Any] = {"button": button}
    if expect_title:
        params["expect_title"] = expect_title

    response = await unity_transport.send_with_unity_instance(
        _legacy_conn.async_send_command_with_retry,
        unity_instance,
        "answer_dialog",
        params,
        retry_on_reload=False,
    )
    return MCPResponse(**response) if isinstance(response, dict) else response
