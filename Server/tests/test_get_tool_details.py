from types import SimpleNamespace

import pytest

from services.tools.get_tool_details import get_tool_details


class FakeTool:
    name = "large_tool"
    description = "Complete original tool description. " * 20
    parameters = {
        "type": "object",
        "required": ["path"],
        "properties": {"path": {"type": "string", "description": "Full path parameter details."}},
    }
    output_schema = {"type": "object", "properties": {"ok": {"type": "boolean"}}}
    annotations = None
    tags = {"group:core"}


class FakeFastMCP:
    async def get_tool(self, name):
        if name != FakeTool.name:
            raise KeyError(name)
        return FakeTool()


@pytest.mark.asyncio
async def test_get_tool_details_returns_full_original_definition():
    result = await get_tool_details(SimpleNamespace(fastmcp=FakeFastMCP()), "large_tool")
    assert result["name"] == "large_tool"
    assert result["description"] == FakeTool.description
    assert result["parameters"] == FakeTool.parameters
    assert result["output_schema"] == FakeTool.output_schema


@pytest.mark.asyncio
async def test_get_tool_details_unknown_name_returns_actionable_error():
    result = await get_tool_details(SimpleNamespace(fastmcp=FakeFastMCP()), "missing_tool")
    assert "Unknown registered tool" in result["error"]


@pytest.mark.asyncio
async def test_get_tool_details_rejects_blank_name():
    result = await get_tool_details(SimpleNamespace(fastmcp=FakeFastMCP()), "  ")
    assert "non-empty" in result["error"]
