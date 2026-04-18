# FieldCure.Mcp.PublicData.Kr

Korean public data API gateway. An [MCP](https://modelcontextprotocol.io) server that lets any MCP client discover, inspect, and call 80,000+ APIs on [data.go.kr](https://www.data.go.kr).

## Tools

| Tool | Description |
|------|-------------|
| `discover_api` | Search data.go.kr APIs by keyword — returns names, providers, endpoint URLs |
| `describe_api` | Get operations, request parameters, and response fields for a specific API |
| `call_api` | Call any data.go.kr API with automatic serviceKey injection and response normalization |

## Quick Start

```bash
dotnet tool install -g FieldCure.Mcp.PublicData.Kr
```

### Prerequisites

1. Sign up at [data.go.kr](https://www.data.go.kr) and get your API key
   (data.go.kr 회원가입 후 인증키 발급)
2. **Subscribe to [목록조회서비스](https://www.data.go.kr/data/15077093/openapi.do)** (Required)
   — `discover_api` and `describe_api` depend on this API
   (discover_api, describe_api 도구가 이 API를 사용합니다)
3. Subscribe to each individual API you want to query
   (조회하려는 개별 API도 각각 활용신청 필요)

## API key resolution

The server resolves the data.go.kr `serviceKey` lazily on the first tool call using the chain
defined in [ADR-001](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/adr/ADR-001-MCP-Credential-Management.md):

1. `--api-key <value>` CLI arg (debug/manual only — not the recommended path)
2. `DATA_GO_KR_API_KEY` environment variable (**primary**)
3. `PUBLICDATA_API_KEY` environment variable (legacy — retained for backward compatibility)
4. **MCP Elicitation** — if the client supports it, the server requests the key via an
   in-session prompt. The resolved key is cached in memory for the session; it is never
   persisted to disk by the server. On `401/403` the cache is invalidated and a re-elicit
   is attempted (capped at 2 retries per session to prevent loops).
5. **Soft-fail** — if none of the above yield a key, `tools/list` still works and each
   tool returns a JSON error explaining that `DATA_GO_KR_API_KEY` must be set or that the
   client must support MCP Elicitation.

### Claude Desktop

```json
{
  "mcpServers": {
    "publicdata-kr": {
      "command": "fieldcure-mcp-publicdata-kr",
      "env": {
        "DATA_GO_KR_API_KEY": "YOUR_DATA_GO_KR_API_KEY"
      }
    }
  }
}
```

### VS Code (Copilot)

```json
{
  "servers": {
    "publicdata-kr": {
      "command": "fieldcure-mcp-publicdata-kr",
      "env": {
        "DATA_GO_KR_API_KEY": "YOUR_DATA_GO_KR_API_KEY"
      }
    }
  }
}
```

### AssistStudio

> **Install the dotnet tool first.** AssistStudio does not auto-install external MCP
> servers. Without the `fieldcure-mcp-publicdata-kr` command on PATH the connection
> fails with a generic "server shut down unexpectedly" message.
>
> ```bash
> dotnet tool install -g FieldCure.Mcp.PublicData.Kr
> ```

Then: Settings > MCP Servers > **Add Server**:

| Field | Value |
|-------|-------|
| **Name** | `PublicData.Kr` |
| **Command** | `fieldcure-mcp-publicdata-kr` |
| **Arguments** | *(empty)* |
| **Environment** | `DATA_GO_KR_API_KEY` = your data.go.kr API key *(optional — will prompt via Elicitation if unset)* |
| **Description** | *(auto-filled on first connection)* |

## Configuration

| Variable | Required | Default | Description |
|----------|:--------:|---------|-------------|
| `DATA_GO_KR_API_KEY` | — | — | data.go.kr API key (인증키). If unset, the server will prompt via MCP Elicitation. |
| `PUBLICDATA_API_KEY` | — | — | Legacy alias for `DATA_GO_KR_API_KEY`. |
| `PUBLICDATA_TIMEOUT_SECONDS` | — | 30 | Per-request timeout |
| `PUBLICDATA_MAX_RESPONSE_LENGTH` | — | 50000 | Max response body length |

> **Naming note:** the API key follows the external service naming convention
> (`DATA_GO_KR_API_KEY`), while server-local configuration uses the `PUBLICDATA_` prefix.
> This keeps the key aligned with data.go.kr's own documentation so users don't have to
> configure it twice, while local tunables stay grouped under a single package namespace.

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).
## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-publicdata)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-publicdata/blob/main/LICENSE)
- [MCP Specification](https://modelcontextprotocol.io)
