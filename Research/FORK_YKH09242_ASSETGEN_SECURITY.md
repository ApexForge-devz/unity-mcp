# Direct Fork Review — ykh09242/unity-mcp AssetGen Download and Archive Security

**Target fork:** [ykh09242/unity-mcp](https://github.com/ykh09242/unity-mcp), default branch `beta`  
**Parent:** CoplayDev/unity-mcp (confirmed from GitHub repository metadata)  
**Source snapshot:** 2026-10-14  
**Review scope:** AssetGen provider artifact downloads, ZIP metadata/extraction, response caps, and async payload lifetime.  
**Important:** source review only; no Unity tests or network integration tests were run during this research pass.

## 1. Why this fork matters

The fork is not a candidate for wholesale replacement: its comparison reports a large ahead count and its recent history includes a sizable unrelated web/agent-task surface. The Unity-specific AssetGen changes are independently useful, however. They address a concrete boundary: provider responses supply URLs and archives that become bytes and, for models, files under the Unity project. Validation must happen before those bytes are downloaded or imported.

Target beta lacks the fork's provider-bound artifact policy, pinned-IP downloader, bounded provider API response handler, ZIP metadata preflight and streamed expansion budgets. The target already has path-containment checks and an extension allowlist; the recommendation is to extend that protection, not replace it.

## 2. Download route in the fork

The route is not merely a helper with no callers:
- `AssetGenJobManager.cs` (fork blob `65ce93ff5b0b74b573fe3424deef2fa412b98e91`) validates a provider result URL and constructs `HttpRequestSpec { Method = "GET", Url = ..., DownloadProvider = r.Job.Provider }` in the download phase.
- `UnityWebRequestTransport.SendAsync` checks `DownloadProvider` and routes artifact results to `AssetDownloadTransport`; ordinary provider API calls use a bounded response handler.
- Model import calls `SafeZipExtractor.ExtractTo` with an allowlist of inert asset formats. Provider archives containing `.cs`, `.dll`, or `.asmdef` are skipped rather than written into `Assets/`.
- `HttpRequestSpec` adds the provider discriminator so artifact downloads cannot be silently treated as ordinary API responses.

That makes the control flow explicit: **provider API call → untrusted URL → provider-specific artifact validation → DNS/address validation → pinned download with byte limit → archive metadata validation → bounded extraction with extension allowlist → Unity import.**

## 3. Source changes worth adapting

### 3.1 Provider-specific URL policy and SSRF resistance

[Fork AssetDownloadPolicy.cs](https://github.com/ykh09242/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Http/AssetDownloadPolicy.cs), blob `bd8c0b7fecdfc05131f75cd1a94839b013049bc2`:
- accepts only absolute HTTPS URLs on port 443 with no backslash, userinfo or fragment;
- requires a DNS hostname and a provider-specific artifact host allowlist;
- explicitly rejects broad shared-storage wildcards such as arbitrary `amazonaws.com` hosts;
- rejects a DNS response if any returned address is non-public, including mixed public/private answers;
- screens IPv4, IPv4-mapped IPv6 and IPv6 special ranges, including loopback/private/link-local, multicast, documentation/reserved, translation/tunnel ranges and Azure's platform virtual IP.

The test suite has examples for shorthand/integer/hex IPv4, localhost, loopback and cloud metadata addresses, userinfo, fragments, unexpected ports, prefix/suffix host lookalikes and provider-host confusion. Unknown providers fail closed. Errors intentionally omit signed URLs and credentials.

**Why this matters:** validating only the URL's textual prefix, or only the first DNS answer, is not enough. A result URL can be returned by a provider API and still be an untrusted outbound destination. Hostname verification must be done again on every redirect.

**Adoption caution:** provider artifact CDN domains are operational contracts. Before adapting the allowlist, independently verify each provider's current documented host/domain and permitted redirect chain. Avoid making the rules so strict that documented provider results are broken, but do not add a generic bypass for signed URLs.

### 3.2 Pinned-IP downloader and redirect loop

[Fork AssetDownloadTransport.cs](https://github.com/ykh09242/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Http/AssetDownloadTransport.cs), blob `0ffa58368fda6cb1a136614c59c6bf37ea8a9b9e`:
- uses one validated DNS snapshot per hop, then connects to an IP literal while preserving the original host for Host/TLS;
- disables proxy use, automatic redirects, default credentials, cookies and connection reuse;
- manually follows only standard redirect status codes, up to five hops, revalidating both provider host and DNS/public-address rules on every hop;
- caps artifact bytes at 512 MiB even when Content-Length is absent or false;
- applies a two-minute cancellation deadline and only retries connect/timeout failures against the same validated DNS address snapshot.

This closes common gaps in “validate DNS, then let another transport resolve the name again” designs. The fork specifically explains that UnityWebRequest does not let this code pin DNS resolution for provider artifacts, so it uses a separate download transport for that path.

**High-priority validation caveat:** the IP-literal + Host/SNI + certificate callback behavior is runtime-sensitive, especially under Unity's Mono/.NET profiles and across Windows/macOS/Linux. The source asserts the intended TLS behavior, but this research has not run that transport against real provider hosts or tested the exact Unity runtime matrix. Treat this as a design candidate requiring network integration tests, not code to cherry-pick blind.

### 3.3 Bounded API responses, not just artifact files

[Fork BoundedDownloadHandler.cs](https://github.com/ykh09242/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Http/BoundedDownloadHandler.cs), blob `d7a30776afec2677fc4c0defe404882ce14809ec`, implements a `DownloadHandlerScript` with a callback receive buffer and 32 MiB default cap. It rejects excessive declared Content-Length and refuses streamed chunks that exceed the cumulative budget. `GetBody()` throws instead of returning a truncated body that could be parsed as a successful provider response.

Target `UnityWebRequestTransport.cs` (blob `ce135375b3e5fff77565fbdc938759cf097bb672`) uses `DownloadHandlerBuffer` for ordinary API responses and has no equivalent global response byte ceiling in the inspected path. That leaves provider API JSON itself as an unbounded buffering path even when the final artifact downloader is constrained.

**Recommendation:** add an explicit, per-route response limit to the shared transport contract. Use a bounded handler/streaming read; preserve the distinction between “complete response within budget” and “body exceeded budget.” Keep a smaller API-response limit than artifact-file limit.

### 3.4 ZIP metadata preflight before archive allocation

[Fork ZipMetadataPreflight.cs](https://github.com/ykh09242/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Import/ZipMetadataPreflight.cs), blob `31e03771e79a4e4a418da7801083d25c3e9b2b28`:
- parses the end-of-central-directory record directly before constructing `ZipArchive`;
- caps central-directory metadata at 16 MiB and entry count at the caller-provided maximum;
- rejects split archives, truncated records, inconsistent counts, bad offsets, oversized record/directory metadata and malformed ZIP64 directory bounds;
- checks actual central-directory entry count against the advertised count and resets the stream before normal archive processing;
- cooperates with cancellation during metadata scanning.

This is a useful complement to streaming extracted bytes. A cap inside the file-copy loop does **not** prevent a hostile archive from advertising a huge central directory/entry count that is allocated while opening the archive.

### 3.5 Bounded, cancellation-aware extraction

[Fork SafeZipExtractor.cs](https://github.com/ykh09242/unity-mcp/blob/beta/MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs), blob `f9b48ec349e608094c79125e8d48f6988720d860`, adds defaults:
- maximum 4,096 entries;
- maximum 512 MiB per expanded entry;
- maximum 2 GiB total expanded bytes;
- maximum 200:1 declared compression ratio;
- cancellation checks during preflight and copying.

It validates advertised sizes before writing files and also counts bytes as each entry stream is copied. It uses `FileMode.CreateNew` to avoid clobbering pre-existing files, checks path containment after normalization, rejects traversal/absolute/colon paths, calls `SafePathUtility.ResolveWithinRoot`, skips disallowed extensions and deletes files created by the current extraction on failure. The model import pipeline passes a narrow allowlist, preventing script/assembly formats from being materialized into project Assets from an untrusted archive.

Target `SafeZipExtractor.cs` (blob `b5ef2b3c00e412c124d0bf76e6524d1b2e52d2dd`) already checks path traversal/root containment and supports a caller-supplied extension allowlist, but its inspected loop has no entry-count, total-expanded-byte or compression-ratio budget and no metadata preflight.

**Review note:** the implementation is careful to delete only files tracked as created by this extraction. It does not establish that every created directory is rolled back after failure; if directory cleanup matters, define ownership of created folders explicitly and test it. Keep the current folder-scoped import and no-overwrite semantics.

### 3.6 Async payload/task lifetime

The fork adds focused EditMode tests that use weak references, large synthetic payloads and explicit GC to assert that:
- completed inline artifact tasks release their result buffers before import, while the written file persists;
- unfinished submit/poll/download tasks remain strongly owned until completion;
- completed, faulted or cancelled task references and request captures are released when safe;
- cancel/failure does not proceed into asset import;
- provider-adapter results remain alive until consumed but can then be released without retaining the entire payload through repeated polling;
- caller-owned request objects are not mutated by submission.

Relevant files:
- [AssetGenConsumedTaskLifetimeTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/AssetGenConsumedTaskLifetimeTests.cs), blob `2d65d120d87decc21284ac715f754d9f09692eaf`.
- [AssetGenProviderPayloadLifetimeTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/AssetGenProviderPayloadLifetimeTests.cs), blob `0f3ba747c980d3ae3701f9297516051d8c26654c`.
- [AssetGenSubmissionCaptureLifetimeTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/AssetGenSubmissionCaptureLifetimeTests.cs), blob `d3f88bb3c24d4bf9e67d7c4e7dfc6dec7791282c`.

These are unusually relevant for Unity's memory pressure during image/model generation because both compressed response buffers and task closures can pin multi-megabyte arrays beyond their useful lifetime. Forced-GC tests are sensitive to JIT/local liveness and need to be structured carefully; still, the tests give a concrete regression strategy rather than relying on code inspection alone.

## 4. New test coverage in the fork

| Test file | Focus |
|---|---|
| [AssetDownloadTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/AssetDownloadTests.cs) | exact approved hosts, rejects spoofed/provider-mismatched URLs, rejects non-public DNS answer sets, validates redirects and pinned-request setup |
| [DownloadLimitSecurityTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/DownloadLimitSecurityTests.cs) | absent/false Content-Length, chunked streamed overflow, response cap before buffering excess |
| [ZipMetadataSecurityTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/ZipMetadataSecurityTests.cs) | inconsistent EOCD values, excessive directory metadata, invalid offsets/truncation, ZIP64 bounds |
| [ArchiveLimitSecurityTests.cs](https://github.com/ykh09242/unity-mcp/blob/beta/TestProjects/UnityMCPTests/Assets/Tests/EditMode/AssetGen/ArchiveLimitSecurityTests.cs) | per-entry/total size, count, compression ratio, cancellation and partial-file rollback |

The source/tests exist in the GitHub tree; they were **not executed by this research session**. The fork's commit/test outcomes and any author-reported pass counts should be checked separately before treating the behavior as validated.

## 5. Adoption decision

**Decision: ADAPT (highest-priority fork finding for P0.3 plus a new P0.4 download boundary). Do not merge the fork wholesale.**

Suggested implementation sequence:
1. Add bounded provider API response handling and explicit response-budget/error semantics.
2. Add compressed and expanded artifact limits, ZIP central-directory preflight, streamed counting, cancellation and partial-file cleanup tests.
3. Route *only provider artifacts* through a narrow provider allowlist and separately reviewed secure downloader; keep authenticated API requests on the API transport, with automatic redirects disabled when carrying credentials.
4. Verify each provider's currently documented artifact URLs and redirects. Test that all provider adapters set `DownloadProvider` only for unauthenticated artifact GETs, never for API status/result JSON.
5. Test each address class, redirect hop and byte limit without credentials; then run integration tests against each legitimate provider on each supported Unity runtime/platform. For the custom pinned-IP transport, explicitly verify hostname/SNI certificate checks and no proxy/DNS rebinding regression.
6. Add lifetime regression cases around task-result ownership/release during submit, polling, cancellation, download and import.

Do not add a user-configurable unrestricted-host fallback. When a provider changes its CDN, add the exact approved hostname with its official documentation reference and tests. Return sanitized errors without signed URLs or API-key material.

## 6. Files and snapshot hashes

| Fork file | Blob SHA |
|---|---|
| AssetDownloadPolicy.cs | `bd8c0b7fecdfc05131f75cd1a94839b013049bc2` |
| AssetDownloadTransport.cs | `0ffa58368fda6cb1a136614c59c6bf37ea8a9b9e` |
| BoundedDownloadHandler.cs | `d7a30776afec2677fc4c0defe404882ce14809ec` |
| HttpRequestSpec.cs | `3832df51611bb46c1e36a3f492abc63c6683a225` |
| ZipMetadataPreflight.cs | `31e03771e79a4e4a418da7801083d25c3e9b2b28` |
| SafeZipExtractor.cs | `f9b48ec349e608094c79125e8d48f6988720d860` |
| AssetGenJobManager.cs | `65ce93ff5b0b74b573fe3424deef2fa412b98e91` |
