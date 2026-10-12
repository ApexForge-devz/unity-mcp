"""On-demand access to an unabridged registered MCP tool definition."""
from typing import Annotated, Any

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool


@mcp_for_unity_tool(
    name="get_tool_details",
    unity_target=None,
    group=None,
    description=(
        "Retrieve a tool's complete original description and JSON Schema "
        "when its compact listing is insufficient."
    ),
    annotations=ToolAnnotations(
        title="Get Tool Details",
        readOnlyHint=True,
        destructiveHint=False,
        idempotentHint=True,
        openWorldHint=False,
    ),
)
async def get_tool_details(
    ctx: Context,
    name: Annotated[str, "Exact MCP tool name whose complete definition is needed."],
) -> dict[str, Any]:
    """Return the full registered definition without executing the selected tool."""
    tool_name = name.strip()
    if not tool_name:
        return {"error": "A non-empty tool name is required."}
    try:
        tool = await ctx.fastmcp.get_tool(tool_name)
    except Exception:
        return {
            "error": f"Unknown registered tool '{tool_name}'.",
            "hint": "Use the name in the optional-group catalog or current tool list.",
        }

    annotations = getattr(tool, "annotations", None)
    if annotations is not None and hasattr(annotations, "model_dump"):
        annotations = annotations.model_dump(exclude_none=True)
    return {
        "name": tool.name,
        "description": tool.description,
        "parameters": tool.parameters,
        "output_schema": getattr(tool, "output_schema", None),
        "annotations": annotations,
        "tags": sorted(getattr(tool, "tags", []) or []),
    }
