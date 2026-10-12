import copy
import json
from types import SimpleNamespace

import pytest

from services.tool_schema_optimizer import (
    ToolSchemaCompactionMiddleware,
    compact_description,
    compact_schema_descriptions,
    compact_tool_for_listing,
)


class FakeTool:
    def __init__(self, *, name, description, parameters, output_schema=None, tags=None):
        self.name = name
        self.description = description
        self.parameters = parameters
        self.output_schema = output_schema
        self.tags = tags or []

    def model_copy(self, *, update):
        values = {
            "name": self.name,
            "description": self.description,
            "parameters": self.parameters,
            "output_schema": self.output_schema,
            "tags": self.tags,
        }
        values.update(update)
        return FakeTool(**values)


def test_compact_description_normalizes_and_caps_long_text():
    assert compact_description(" short   text ", 20) == "short text"
    compact = compact_description("Purpose.\n\n  More detail " * 20, 70)
    assert len(compact) <= 70
    assert compact.endswith("…")
    assert "\n" not in compact


def test_schema_compaction_preserves_structure_and_validation_constraints():
    original = {
        "type": "object",
        "required": ["mode"],
        "properties": {
            "mode": {
                "type": "string",
                "enum": ["read", "write"],
                "default": "read",
                "description": "Long mode description " * 20,
            },
            "count": {
                "type": "integer",
                "minimum": 1,
                "maximum": 8,
                "description": "A short number.",
            },
        },
        "additionalProperties": False,
    }
    before = copy.deepcopy(original)
    compact = compact_schema_descriptions(original, max_chars=70)
    assert original == before
    assert compact["type"] == original["type"]
    assert compact["required"] == original["required"]
    assert compact["additionalProperties"] is False
    assert compact["properties"]["mode"]["enum"] == ["read", "write"]
    assert compact["properties"]["mode"]["default"] == "read"
    assert compact["properties"]["count"]["minimum"] == 1
    assert compact["properties"]["count"]["maximum"] == 8
    assert len(compact["properties"]["mode"]["description"]) <= 70
    assert compact["properties"]["count"]["description"] == "A short number."


def test_tool_compaction_returns_copy_and_reduces_payload():
    schema = {
        "type": "object",
        "required": ["path"],
        "properties": {"path": {"type": "string", "description": "Path description " * 25}},
    }
    original = FakeTool(
        name="large_tool",
        description="Tool description " * 30,
        parameters=schema,
        output_schema={"type": "object", "description": "Output details " * 30},
        tags=["group:core"],
    )
    original_description = original.description
    original_schema = copy.deepcopy(original.parameters)
    compact = compact_tool_for_listing(original)
    assert compact is not original
    assert original.description == original_description
    assert original.parameters == original_schema
    assert len(compact.description) < len(original.description)
    assert len(compact.parameters["properties"]["path"]["description"]) < len(
        original.parameters["properties"]["path"]["description"]
    )
    assert compact.parameters["required"] == original.parameters["required"]
    assert compact.parameters["properties"]["path"]["type"] == "string"
    assert len(compact.output_schema["description"]) < len(original.output_schema["description"])
    assert compact.tags == original.tags
    assert len(json.dumps(compact.parameters)) < len(json.dumps(original.parameters))


@pytest.mark.asyncio
async def test_middleware_compacts_tool_list_without_mutating_registered_tool():
    original_description = "Detailed tool behavior " * 30
    registered = FakeTool(
        name="fixture",
        description=original_description,
        parameters={"type": "object", "properties": {"n": {"type": "integer"}}},
    )
    middleware = ToolSchemaCompactionMiddleware()

    async def call_next(_context):
        return [registered]

    listed = await middleware.on_list_tools(SimpleNamespace(), call_next)
    assert listed[0].description != registered.description
    assert registered.description == original_description
