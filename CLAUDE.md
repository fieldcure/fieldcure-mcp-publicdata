# FieldCure.Mcp.PublicData.Kr

MCP server that bridges LLM clients to 80,000+ Korean public data APIs on
[data.go.kr](https://www.data.go.kr). Distributed as a .NET tool
(`fieldcure-mcp-publicdata-kr`) and NuGet package
([`FieldCure.Mcp.PublicData.Kr`](https://www.nuget.org/packages/FieldCure.Mcp.PublicData.Kr)).

## Solution layout

```
src/FieldCure.Mcp.PublicData.Kr/
├── Program.cs                     # stdio MCP server entry point (no startup hard-fail)
├── Services/
│   ├── ApiKeyResolver.cs          # env var → Elicitation → soft-fail chain + session cache
│   ├── InvalidApiKeyException.cs  # Signals upstream auth rejection (401/403 or body)
│   ├── KeyedCall.cs               # resolve → run → invalidate → retry helper
│   ├── PublicDataHttpClient.cs    # HTTP proxy with serviceKey injection
│   ├── DomainWhitelist.cs         # SSRF prevention via host whitelist
│   ├── ResponseNormalizer.cs      # XML → JSON normalization, EUC-KR support
│   └── ErrorCodeMapper.cs         # Korean guidance for data.go.kr result codes
└── Tools/
    ├── DiscoverApiTool.cs         # discover_api
    ├── DescribeApiTool.cs         # describe_api
    └── CallApiTool.cs             # call_api

tests/FieldCure.Mcp.PublicData.Kr.Tests/   # MSTest — DomainWhitelist, ErrorCodeMapper, ResponseNormalizer
```

## Build & test

```bash
dotnet build                        # build the project
dotnet test                         # run MSTest suite
pwsh scripts/publish-nuget.ps1      # pack → sign (EV dongle) → push
pwsh scripts/publish-nuget.ps1 -SkipSign -SkipPush   # local pack only
```

`publish-nuget.ps1` expects a Fieldcure GlobalSign code-signing cert on the USB dongle and a
`NUGET_API_KEY` env var (or `-NuGetApiKey`). Never bypass signing unless explicitly asked.

## Credential model (ADR-001)

This repo is the **Phase 1 pilot** for
[ADR-001 MCP Credential Management](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/ADR-001-MCP-Credential-Management.md).
The principles below must hold for any future change; they also apply verbatim to the
Phase 2 (Essentials v2.1) and Phase 3 (Outbox v2.0) rollouts.

### Resolution chain (lazy, per tool call)

```
env var  (DATA_GO_KR_API_KEY  >  legacy PUBLICDATA_API_KEY)
   →  MCP Elicitation  (interactive fallback on Elicitation-capable clients)
       →  soft-fail JSON envelope  (tools/list still works)
```

`--api-key` CLI arg is a debug escape hatch only — not a supported production path.

### Core invariants (do not weaken)

1. **Server is stateless for secrets.** Elicited keys live only in
   `ApiKeyResolver._cachedKey` (process memory). Never write keys to disk; persistence is
   the host's responsibility (AssistStudio uses PasswordVault, Claude Desktop uses its
   MCP config, CI/Docker use env vars).
2. **Lazy resolution.** `tools/list` must succeed with no key configured. Startup never
   blocks on credentials. Only tool invocations trigger the resolver.
3. **Soft fail, not hard fail.** A missing key surfaces as a structured error envelope
   naming the expected env var; the server keeps running. Hard-failing at startup would
   break clients that discover tools before configuring auth.
4. **Self-recovery on auth rejection.** Both HTTP 401/403 and body signals
   (`resultCode=22`, `SERVICE_KEY_IS_NOT_REGISTERED_ERROR`, `UNAUTHORIZED_KEY`) raise
   `InvalidApiKeyException`. `KeyedCall.RunAsync` catches once and does a single retry.
   `ApiKeyResolver.MaxReElicits = 2` caps total re-elicits per resolver lifetime to
   prevent infinite loops.
5. **Env var naming follows the external service convention.** `DATA_GO_KR_API_KEY` is
   the canonical name for the data.go.kr API family; users who already have this set
   from other tools should not have to rename. Server-local tunables use the
   `PUBLICDATA_` prefix (`PUBLICDATA_TIMEOUT_SECONDS`, `PUBLICDATA_MAX_RESPONSE_LENGTH`).
   Do not namespace external-service keys with `FIELDCURE_*`.

### Where the wiring lives

- `ApiKeyResolver`: chain, cache, `MaxReElicits`, `Invalidate()`, thread-safe via
  `SemaphoreSlim`. After an invalidate the resolver skips static sources (CLI + env var)
  because a 401 proves them bad; re-reading the same env var would just cache the same
  broken key.
- `KeyedCall.RunAsync(server, resolver, operation, ct)`: the single place tools go
  through. Handles resolve → operate → (on invalid-key exception) invalidate → resolve
  again → retry once → surface error envelope.
- `PublicDataHttpClient`: per-method `apiKey` parameter (no stored key in the client),
  throws `InvalidApiKeyException` on HTTP 401/403, runs `ThrowIfBodySignalsInvalidKey`
  after normalization for the 200-OK-with-error-body case.
- Tools inject `McpServer`, `PublicDataHttpClient`, `ApiKeyResolver` via the MCP SDK's
  parameter-binding rules; positional args come from the tool call JSON.

## Coding conventions

- C# 12, nullable enable, implicit usings, `net8.0` target.
- `<GenerateDocumentationFile>` on — every public and most internal members carry `///`
  XML docs. Keep them accurate when refactoring.
- Tools: static classes with `[McpServerToolType]`; methods with
  `[McpServerTool(Name = "...")]` and a `[Description(...)]` that reads naturally to an
  LLM (it ends up in `tools/list`).
- Responses: normalize to JSON via `ResponseNormalizer`. Error envelopes look like
  `{"error": "..."}` (and optionally `"error_code": "..."` for mapped codes) — stay
  consistent so `LooksLikeErrorEnvelope` heuristics in tools keep working.
- Commits: English, focused (one logical change per commit). Never amend already-pushed
  commits; create follow-ups.

## Known trade-offs and Phase 1.1 follow-ups

These are deliberate — do not "fix" without reading the context first.

1. **HTTP 401/403 is ambiguous on data.go.kr.** It surfaces both for genuinely invalid
   keys and for APIs the user has not applied for (활용신청). The current behaviour treats
   every 401/403 as an invalid-key signal, which costs one unnecessary re-elicit on the
   unsubscribed-API case in exchange for auto-recovery on the invalid-key case. Real
   testing showed completely invalid keys *do* return HTTP 401, so dropping 401 entirely
   (a brief experiment, now reverted) was the wrong tradeoff. Phase 1.1: inspect the
   response body on 401/403 and only invalidate when it carries an invalid-key signal.
2. **ODCloud catalog returns HTTP 400 for bad keys.** `discover_api` and `describe_api`
   hit `api.odcloud.kr`, which does not use `resultCode=22`. They currently cannot
   self-recover from an invalid key. Phase 1.1: body-signal detection in
   `GetStringAsync` for the ODCloud JSON error format.
3. **MCP SDK 1.2 has no `format: "password"` on `StringSchema`.** Only `email`/`uri`/
   `date`/`date-time` are accepted. Hosts render the `api_key` elicit field however
   they like; Claude Code v2.1.113 shows it in plain text, AssistStudio's
   `ToolElicitationPanel` masks via a name/title keyword heuristic. Track upstream MCP
   spec progress for a canonical masked-input hint.
4. **Claude Code restarts idle stdio subprocesses.** The in-memory key cache clears
   between tool calls in some sessions, causing repeated Elicitation prompts. This is
   host policy, not a server bug; stateless-server invariant (§1) holds. Users should
   set the env var when using Claude Code for long sessions.

## Related repos

- [AssistStudio](https://github.com/fieldcure/fieldcure-assiststudio) — primary desktop
  host and the reference Elicitation client. `ToolElicitationPanel` in the
  `AssistStudio.Controls` package applies the secret-field masking heuristic described
  above.
- [FieldCure.Mcp.Essentials](https://github.com/fieldcure/fieldcure-mcp-essentials) —
  Phase 2 ADR target; will add Elicitation to its existing `ResolveArg` chain.
- [FieldCure.Mcp.Outbox](https://github.com/fieldcure/fieldcure-mcp-outbox) — Phase 3
  ADR target; will replace its `add_channel` subprocess-console pattern with
  Elicitation-based multi-field flows.
