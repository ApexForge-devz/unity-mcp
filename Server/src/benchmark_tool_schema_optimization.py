"""Measure compacted vs original registered MCP tool-list payload size.

Run from the Server directory:
    uv run python src/benchmark_tool_schema_optimization.py

This local schema-serialization benchmark does not connect to GitHub or Unity.
"""
from __future__ import annotations

import asyncio
import json
from collections import defaultdict
from typing import Any

from main import create_mcp_server
from services.registry import get_registered_tools
from services.tool_schema_optimizer import compact_tool_for_listing


def _serialize_tool(tool: Any) -> str:
    return json.dumps(
        tool.to_mcp_tool().model_dump(mode="json", by_alias=True, exclude_none=True),
        ensure_ascii=False,
        separators=(",", ":"),
    )


async def main() -> None:
    server = create_mcp_server(project_scoped_tools=True)
    names = sorted({item["name"] for item in get_registered_tools()})
    tools: list[Any] = []
    for name in names:
        try:
            tools.append(await server.get_tool(name))
        except Exception:
            continue

    grouped: dict[str, list[Any]] = defaultdict(list)
    for tool in tools:
        tags = getattr(tool, "tags", []) or []
        group = next((tag.removeprefix("group:") for tag in tags
                      if isinstance(tag, str) and tag.startswith("group:")), "server")
        grouped[group].append(tool)

    originals = [_serialize_tool(tool) for tool in tools]
    compacted = [_serialize_tool(compact_tool_for_listing(tool)) for tool in tools]
    original_bytes = sum(len(value.encode("utf-8")) for value in originals)
    compact_bytes = sum(len(value.encode("utf-8")) for value in compacted)
    saved = original_bytes - compact_bytes
    report = {
        "tool_count": len(tools),
        "original_schema_bytes": original_bytes,
        "compacted_schema_bytes": compact_bytes,
        "bytes_saved": saved,
        "reduction_percent": round(100 * saved / original_bytes, 2) if original_bytes else 0.0,
        "estimated_original_tokens_4_chars": round(original_bytes / 4),
        "estimated_compacted_tokens_4_chars": round(compact_bytes / 4),
        "groups": {},
    }
    for group in sorted(grouped):
        raw = sum(len(_serialize_tool(tool).encode("utf-8")) for tool in grouped[group])
        short = sum(len(_serialize_tool(compact_tool_for_listing(tool)).encode("utf-8"))
                    for tool in grouped[group])
        report["groups"][group] = {
            "tools": len(grouped[group]),
            "original_bytes": raw,
            "compacted_bytes": short,
            "bytes_saved": raw - short,
        }
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    asyncio.run(main())
