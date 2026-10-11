---
title: manage_tools
sidebar_label: manage_tools
description: "Manage which tool groups are visible in this session."
---

# `manage_tools`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_tools`

## Description

List or manage session-visible tool groups. Plan the task first, then use groups=[...] to activate or deactivate several optional groups in one call. Core is available directly. Use sync only after changing the Unity Editor tool toggles.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['list_groups', 'activate', 'deactivate', 'sync', 'reset']` | yes | Action to perform. |
| `group` | `str \| None` | — | One group for activate/deactivate. Use groups for batches. |
| `groups` | `list[str] \| None` | — | Multiple group names to activate/deactivate in one call. Use either group or groups, not both. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

