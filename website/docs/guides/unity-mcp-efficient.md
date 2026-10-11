---
title: Unity MCP Efficient
sidebar_label: Unity MCP Efficient
description: Use the optional Unity MCP Efficient facade with this Unity 6 enhancement fork.
---

# Unity MCP Efficient

[Unity MCP Efficient](https://github.com/Vangardo/unity-mcp-efficient) is an independent MCP facade that exposes a compact six-tool surface and discovers Unity operations on demand. It connects to the Unity MCP **Python HTTP server**; it does not replace the Unity Editor package.

This integration is optional. Clients that connect directly to this fork keep the ordinary Unity MCP tool catalog.

## 1. Install this fork's Unity package

In Unity Package Manager, use **Add package from git URL**:

```text
https://github.com/ApexForge-devz/unity-mcp.git?path=/MCPForUnity#feature/unity6-enhancements
```

This branch targets Unity 6.0 and later.

## 2. Run this fork's Python server over local HTTP

Use the checkout of this fork, not the published `mcpforunityserver` package, so the server-side changes from this branch are available.

```powershell
cd unity-mcp\Server
uv sync
uv run src/main.py --transport http --http-host 127.0.0.1 --http-port 8080
```

Keep the Unity Editor package connected to this running server. The default MCP endpoint is:

```text
http://127.0.0.1:8080/mcp
```

Keep the backend bound to loopback for local use. Do not expose an unauthenticated HTTP endpoint to an untrusted network.

## 3. Register the facade with your MCP client

For Codex, use the versioned companion release:

```powershell
codex mcp add unity-efficient --env UNITY_MCP_BACKEND_URL=http://127.0.0.1:8080/mcp -- uvx --from git+https://github.com/Vangardo/unity-mcp-efficient.git@v0.2.0 unity-mcp-efficient
```

For clients that accept an MCP server configuration, use this shape:

```json
{
  "mcpServers": {
    "unity-efficient": {
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/Vangardo/unity-mcp-efficient.git@v0.2.0",
        "unity-mcp-efficient"
      ],
      "env": {
        "UNITY_MCP_BACKEND_URL": "http://127.0.0.1:8080/mcp"
      }
    }
  }
}
```

Disable the direct Unity MCP server entry **in the same AI client** when using the facade. Keep the Unity package and its HTTP backend running. Registering both the full server and the facade defeats most of the smaller-tool-surface benefit.

## Compatibility and verification

The companion project's published compatibility tests target MCP for Unity **v10.1.0**. This fork is based on **v10.3.1-beta.7** with additional changes, and compatibility between those versions has **not** been independently verified in this repository. A dynamic operation catalog helps, but it is not a compatibility guarantee.

After connecting, verify read-only operations first: inspect the Editor state, search for a relevant operation, inspect the active scene, and retrieve the full result for any compact/truncated output. Exercise mutation, batching, screenshots, and modal-dialog recovery only after those basics work in your client.

The companion README reports substantial context reductions for its own benchmark fixtures; those are measurements against v10.1.0, not guarantees for every model, scene, or payload. See the [upstream README](https://github.com/Vangardo/unity-mcp-efficient#measured-context-reduction) and [benchmark methodology](https://github.com/Vangardo/unity-mcp-efficient/blob/v0.2.0/BENCHMARKS.md).

The facade is a third-party project and is not vendored into this repository. Review its [security notes](https://github.com/Vangardo/unity-mcp-efficient/blob/v0.2.0/SECURITY.md) before exposing its HTTP or SSE transport.
