# Release Notes

## v1.0.2 (2026-04-22)

### Changed

- **`call_api` description** — rewritten to spell out the `discover_api` → `describe_api` → `call_api` workflow, explicitly forbid passing a serviceId from `discover_api` as input, and document that the `params` argument is a JSON STRING (not an object) with a concrete example. The per-parameter `url` and `params` descriptions repeat those constraints at the argument level so schema validators surface them.
- **`discover_api` description** — clarifies that the returned `serviceId` is the input for `describe_api`, not for `call_api`, and that `describe_api` must be called before `call_api`.
- **`describe_api` description** — states that it returns `operations[].url` and `request_parameters[]`, which are the required inputs for `call_api`, and that a serviceId must never be passed directly to `call_api`.

### Context

External hosts (AssistStudio) now dispatch MCP tools through a `search_tools` + `invoke_tool` pair rather than a per-step sub-agent. In that flow the main conversation does not automatically loop `discover_api` → `describe_api`, so models sometimes called `call_api` with a guessed schema (`service_id` and a raw `parameters` object) and hit a generic server failure on every call. Baking the workflow and parameter shape into the tool descriptions lets the model choose correct arguments without an extra schema-discovery round-trip — no code behaviour changed.

---

## v1.0.1 (2026-04-20)

- Update MCP package metadata to the latest `server.json` format for NuGet and VS Code integration.

## v1.0.0 (2026-04-18)

**ADR-001 Phase 1: Lazy credential resolution.** Pilot implementation of the FieldCure MCP credential management ADR. Non-breaking for existing users who set an env var.

### Added

- **`ApiKeyResolver`** service — resolves the `serviceKey` via the priority chain: CLI arg (`--api-key`, debug only) → `DATA_GO_KR_API_KEY` env var → `PUBLICDATA_API_KEY` legacy env var → **MCP Elicitation** (spec 2025-06-18+) → soft-fail. The resolved key is cached in process memory for the session; it is never persisted to disk by the server.
- **`InvalidApiKeyException`** — thrown by the HTTP client on upstream 401/403 so tools can trigger cache invalidation and a single retry.
- **`KeyedCall`** helper — wraps tool invocations with the resolve → run → (on invalid key) invalidate + retry loop. Session-level re-elicit cap of 2 prevents loops.
- **Soft-fail path** — the server no longer aborts startup when no key is configured. `tools/list` always responds; per-tool calls return a structured error pointing at `DATA_GO_KR_API_KEY` and noting the Elicitation fallback.

### Changed

- **Env var rename**: `PUBLICDATA_API_KEY` → `DATA_GO_KR_API_KEY` (canonical name matching external Korean government API conventions). The legacy name continues to work as a fallback and is documented as such in `.mcp/server.json` and README.
- **`PublicDataHttpClient`** no longer owns the API key. Each public method accepts an `apiKey` parameter supplied by the caller (resolved via `ApiKeyResolver`). The client throws `InvalidApiKeyException` on HTTP 401/403.
- **`Program.cs`** — removed the `throw new InvalidOperationException("PUBLICDATA_API_KEY is required")` guard. Startup only reads `--api-key` as an optional seed for the resolver; env var and Elicitation are evaluated lazily at tool call time.
- **Package version** bumped to **1.0.0** (initial release on the ADR-001 credential management strategy).

### Migration

Existing users who had set `PUBLICDATA_API_KEY` need no action — the legacy env var continues to be read. To adopt the canonical name, rename to `DATA_GO_KR_API_KEY`. Hosts that support MCP Elicitation (e.g. AssistStudio) may skip env var configuration entirely; the server will request the key on first tool use.

---

## v0.2.0 (2026-04-14)

### Changed

- **Centralize `JsonSerializerOptions`** — extract shared `McpJson.Options` to eliminate per-tool serializer configuration duplication
- **Docs** — inline `PackageReleaseNotes` in csproj, rename Ecosystem section, add GitHub Releases workflow

---

## v0.1.0 (2026-04-05)

Initial release.

### Features

- **3 MCP tools** for Korean public data access via stdio transport
  - **`discover_api`** — search data.go.kr APIs by keyword with `list_id` deduplication
  - **`describe_api`** — get operations, request parameters, and response fields for a specific API
  - **`call_api`** — call any data.go.kr API with automatic `serviceKey` injection and response normalization
- **XML → JSON normalization** — strips `response/header/body/items` wrapper, returns clean JSON
- **Error code mapping** — translates data.go.kr error codes (12, 20, 22, 30, 31) into Korean guidance messages
- **SSRF protection** — domain whitelist limits calls to approved government hosts (`api.odcloud.kr`, `apis.data.go.kr`, `api.data.go.kr`, `openapi.data.go.kr`, `www.law.go.kr`, `open.neis.go.kr`)
- **API key masking** — `serviceKey` replaced with `***` in all error output
- **EUC-KR support** — legacy encoding from older government APIs auto-detected and converted
- **dotnet tool** packaging for global installation via NuGet

### Tech Stack

- .NET 8.0
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) v1.2.0
- Microsoft.Extensions.Hosting
- MSTest (35 tests)
