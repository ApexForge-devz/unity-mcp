"""Conservative, list-only MCP tool schema compaction.

Only free-text descriptions are shortened. Parameter names, schema structure,
types, required fields, defaults, enums, and validation constraints are untouched.
Full original definitions remain in FastMCP's registry and can be retrieved with
the get_tool_details meta-tool.
"""
from __future__ import annotations

import re
from typing import Any

from fastmcp.server.middleware import Middleware, MiddlewareContext


_WHITESPACE = re.compile(r"\s+")
ELLIPSIS = "…"


def compact_description(value: Any, max_chars: int) -> Any:
    """Normalize description whitespace and cap long prose at a word boundary."""
    if not isinstance(value, str):
        return value
    normalized = _WHITESPACE.sub(" ", value).strip()
    if len(normalized) <= max_chars or max_chars < 12:
        return normalized
    prefix = normalized[: max_chars - len(ELLIPSIS)]
    boundary = prefix.rfind(" ")
    if boundary >= int(max_chars * 0.65):
        prefix = prefix[:boundary]
    return prefix.rstrip(" ,;:-.") + ELLIPSIS


def compact_schema_descriptions(schema: Any, max_chars: int = 140) -> Any:
    """Copy a JSON Schema, shortening descriptions while preserving all other data."""
    if isinstance(schema, dict):
        result: dict[str, Any] = {}
        for key, value in schema.items():
            if key == "description" and isinstance(value, str):
                result[key] = compact_description(value, max_chars)
            else:
                result[key] = compact_schema_descriptions(value, max_chars)
        return result
    if isinstance(schema, list):
        return [compact_schema_descriptions(item, max_chars) for item in schema]
    return schema


def compact_tool_for_listing(
    tool: Any,
    *,
    tool_description_chars: int = 220,
    parameter_description_chars: int = 140,
) -> Any:
    """Return a compact copy; never mutate the registered tool."""
    updates: dict[str, Any] = {
        "description": compact_description(
            getattr(tool, "description", None), tool_description_chars
        ),
        "parameters": compact_schema_descriptions(
            getattr(tool, "parameters", {}), parameter_description_chars
        ),
    }
    output_schema = getattr(tool, "output_schema", None)
    if output_schema is not None:
        updates["output_schema"] = compact_schema_descriptions(
            output_schema, parameter_description_chars
        )
    model_copy = getattr(tool, "model_copy", None)
    return model_copy(update=updates) if callable(model_copy) else tool


class ToolSchemaCompactionMiddleware(Middleware):
    """Compact long descriptions only when tools are listed to an MCP client."""

    async def on_list_tools(
        self,
        context: MiddlewareContext,
        call_next,
    ) -> list[Any]:
        tools = await call_next(context)
        return [compact_tool_for_listing(tool) for tool in tools]
