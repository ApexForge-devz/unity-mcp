# Upstream PR #1461 — Custom MCP Server Instructions from a Startup File

**Reviewed:** 2026-10-15  
**PR:** [CoplayDev/unity-mcp #1461](https://github.com/CoplayDev/unity-mcp/pull/1461)  
**Target baseline:** beta `142ea0af4890337f0ff6658df61db23982313717`  
**Candidate head:** `8df0366170eab767f9e56a1d7a3b81d84546a092`  
**Status:** Open, not merged  
**Decision:** **ADAPT conditionally for server-wide conventions; do not use it for project isolation or enforcement.**  
**Review method:** Source/test inspection only; no tests run independently.

## Executive assessment

PR #1461 adds `--instructions-file PATH` and `UNITY_MCP_INSTRUCTIONS_FILE` to append UTF-8 text to the built-in MCP server instructions in the MCP `initialize` response. It preserves the built-in instructions, applies to stdio and HTTP, accepts a UTF-8 BOM, and stops startup with a generic configuration error if the file cannot be read.

This can carry stable agent guidance such as a deterministic scene-generation/regeneration workflow without modifying the server source. But the text is loaded once per server and applies to every client and Unity project connected to that server. It is therefore suitable for shared conventions or a server isolated to one project, not per-project instructions on a multi-project server.

## 1. Source-verified behavior

The candidate changes `Server/src/main.py`:

- Adds the CLI option and environment variable; the CLI option takes precedence.
- Expands `~`; relative paths resolve from the server working directory.
- Reads once at startup using `encoding="utf-8-sig"`, strips whitespace and appends non-empty text under an “Additional instructions” heading to the built-in FastMCP instruction string.
- Empty/whitespace-only files leave the built-in instructions unchanged.
- Missing, unreadable or invalid UTF-8 input stops startup before the MCP server is created/run. The configuration error omits the path and file contents.
- Both stdio and HTTP use the same server initialization instructions; no per-client/per-Unity-instance instruction selection is added.

The candidate adds `Server/tests/test_server_instructions.py` covering the built-in instructions, empty/no file, both transports, CLI/environment precedence, UTF-8 BOM, missing/unreadable/invalid-encoding errors and command help.

The PR author reports 1,598 Python tests passed with 18 skipped, 33 targeted tests passed and manual stdio/HTTP initialization checks. This is author-reported evidence, not independently rerun here; Windows/Linux and differing client handling of server instructions were not verified.

## 2. Fit with deterministic scene authoring

On a server dedicated to the target project, a short instruction file could reinforce these stable conventions:

- the persistent scene-generation/regeneration script is the source of truth;
- edit the script, compile/reload, regenerate and save the explicit output scene;
- do not repair generated scenes through direct per-GameObject/component CRUD calls;
- validate with narrow assertions and existing screenshot/read-only diagnostics;
- reconcile uncertain generator outcomes by run/status identity rather than blindly rerunning;
- follow the project's paths, naming, asset-reference and test conventions.

This can be maintained without forking `main.py`. It should complement—not replace—project/client instruction files such as `CLAUDE.md` or repository agent guidance. The MCP client decides how server instructions are presented and followed.

## 3. Critical scope and safety boundaries

### 3.1 The text is server-wide, not project-scoped

One startup file feeds one FastMCP `instructions` string. If several Unity projects or clients share the server, project-specific instructions such as “only modify Assets/SlimeVerse” may be wrong for another project, and any private context in the file is sent to all clients receiving those initialization instructions.

Use this only for non-sensitive conventions common to all connected projects, or run a separate server dedicated to the intended project. Do not put credentials, private data, customer content or project-private paths in a shared instructions file. For project-specific rules on a shared server, use project/client-level instruction configuration instead.

### 3.2 Instructions are not enforcement

The feature changes model-facing guidance, not server permissions. Clients may handle instructions differently, and the text cannot prevent a tool call. File/path security, project routing, tool validation and operation safety must remain enforced in server/Unity code. Do not treat a prompt instruction as an access-control boundary.

### 3.3 Startup-only loading

The file is read once during startup. Editing it requires restarting the server and reconnecting/reinitializing the client. Do not promise hot reload. The fail-fast behavior for unreadable/invalid files is appropriate because it avoids silently starting with missing intended guidance; retain the test that no server instance is created after failure.

## Recommended decision

**ADAPT conditionally at P2.** Use the feature for common team/server procedures or a server dedicated to the target project. For the script-first workflow, keep the custom instructions concise and refer to project documentation rather than copying large manuals into the initialization payload. On shared/multi-project servers, keep project-specific rules in client/repository instructions instead.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| No file configured | Built-in instructions remain unchanged. |
| Valid UTF-8 file | Trimmed text appended once; default resource/workflow instructions remain. |
| UTF-8 BOM / empty file | BOM stripped; blank text adds nothing. |
| CLI option and environment both set | CLI path takes precedence. |
| Missing path, directory or invalid UTF-8 | Startup fails before server creation/run; error leaks neither path nor contents. |
| Stdio and HTTP | Same instruction content delivered through both transports. |
| Multiple projects on one server | Instructions are understood as global; no project-level isolation is implied. |
| File changes after startup | Existing server uses the original loaded contents until restart/reconnect. |
| Client ignores instructions | No safety/access-control guarantee is inferred. |

## Source references

- [PR #1461](https://github.com/CoplayDev/unity-mcp/pull/1461)
- Candidate [main.py](https://github.com/CoplayDev/unity-mcp/blob/8df0366170eab767f9e56a1d7a3b81d84546a092/Server/src/main.py)
- Candidate [test_server_instructions.py](https://github.com/CoplayDev/unity-mcp/blob/8df0366170eab767f9e56a1d7a3b81d84546a092/Server/tests/test_server_instructions.py)
- Candidate [Server README](https://github.com/CoplayDev/unity-mcp/blob/8df0366170eab767f9e56a1d7a3b81d84546a092/Server/README.md)
- Target [main.py](https://github.com/ApexForge-devz/unity-mcp/blob/beta/Server/src/main.py)
