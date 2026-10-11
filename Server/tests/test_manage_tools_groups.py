import pytest

from services.tools.manage_tools import manage_tools


class FakeContext:
    def __init__(self, rules=None):
        self.rules = list(rules or [])
        self.enable_calls = []
        self.disable_calls = []
        self.info_messages = []
        self.reset_calls = 0

    async def _get_visibility_rules(self):
        return list(self.rules)

    async def enable_components(self, *, tags, components):
        self.enable_calls.append((set(tags), set(components)))
        self.rules.append({"tags": sorted(tags), "enabled": True})

    async def disable_components(self, *, tags, components):
        self.disable_calls.append((set(tags), set(components)))
        self.rules.append({"tags": sorted(tags), "enabled": False})

    async def info(self, message):
        self.info_messages.append(message)

    async def reset_visibility(self):
        self.reset_calls += 1
        self.rules.clear()


@pytest.mark.asyncio
async def test_batch_activation_changes_multiple_groups_in_one_visibility_rule():
    ctx = FakeContext()

    result = await manage_tools(
        ctx,
        action="activate",
        groups=[" UI ", "testing", "ui"],
    )

    assert result["activated_groups"] == ["ui", "testing"]
    assert result["changed_groups"] == ["ui", "testing"]
    assert result["unchanged_groups"] == []
    assert set(result["tools_by_group"]) == {"ui", "testing"}
    assert ctx.enable_calls == [
        ({"group:ui", "group:testing"}, {"tool"})
    ]
    assert ctx.disable_calls == []


@pytest.mark.asyncio
async def test_batch_activation_skips_groups_already_enabled():
    ctx = FakeContext([
        {"tags": ["group:testing"], "enabled": True},
    ])

    result = await manage_tools(
        ctx,
        action="activate",
        groups=["testing", "profiling"],
    )

    assert result["changed_groups"] == ["profiling"]
    assert result["unchanged_groups"] == ["testing"]
    assert ctx.enable_calls == [
        ({"group:profiling"}, {"tool"})
    ]


@pytest.mark.asyncio
async def test_activation_of_already_enabled_single_group_is_a_noop():
    ctx = FakeContext([
        {"tags": ["group:testing"], "enabled": True},
    ])

    result = await manage_tools(ctx, action="activate", group="testing")

    assert result["activated"] == "testing"
    assert result["changed"] is False
    assert ctx.enable_calls == []
    assert "already visible" in result["message"]


@pytest.mark.asyncio
async def test_invalid_batch_is_rejected_atomically():
    ctx = FakeContext()

    result = await manage_tools(
        ctx,
        action="activate",
        groups=["ui", "not_a_group"],
    )

    assert "not_a_group" in result["error"]
    assert "No changes were made" in result["error"]
    assert ctx.enable_calls == []
    assert ctx.disable_calls == []


@pytest.mark.asyncio
async def test_single_and_multiple_group_arguments_cannot_be_combined():
    ctx = FakeContext()

    result = await manage_tools(
        ctx,
        action="activate",
        group="ui",
        groups=["testing"],
    )

    assert "not both" in result["error"]
    assert ctx.enable_calls == []


@pytest.mark.asyncio
async def test_batch_deactivation_uses_one_visibility_rule():
    ctx = FakeContext([
        {"tags": ["group:ui", "group:testing"], "enabled": True},
    ])

    result = await manage_tools(
        ctx,
        action="deactivate",
        groups=["ui", "testing"],
    )

    assert result["deactivated_groups"] == ["ui", "testing"]
    assert result["changed_groups"] == ["ui", "testing"]
    assert ctx.disable_calls == [
        ({"group:ui", "group:testing"}, {"tool"})
    ]
