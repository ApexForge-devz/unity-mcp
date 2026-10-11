---
title: get_tool_details
sidebar_label: get_tool_details
description: Retrieve the complete original description and JSON Schema for a registered tool.
---

# `get_tool_details`

Returns a tool's complete registered description, input JSON Schema, optional output schema, annotations, and tags. This tool does not execute the selected tool.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `str` | yes | Exact MCP tool name whose complete definition is needed. |

## Usage

Call this only when the compact tool-list description or parameter descriptions do not provide enough information to safely select arguments. The tool list itself is compacted for token efficiency; this call returns the original registered definition.
