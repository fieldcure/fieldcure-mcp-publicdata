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

### Claude Desktop

```json
{
  "mcpServers": {
    "publicdata-kr": {
      "command": "fieldcure-mcp-publicdata-kr",
      "env": {
        "PUBLICDATA_API_KEY": "YOUR_DATA_GO_KR_API_KEY"
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
        "PUBLICDATA_API_KEY": "YOUR_DATA_GO_KR_API_KEY"
      }
    }
  }
}
```

### AssistStudio

Settings → MCP Servers → Add Server:

| Field | Value |
|-------|-------|
| Name | 한국 공공데이터 |
| Command | `fieldcure-mcp-publicdata-kr` |
| Environment | `PUBLICDATA_API_KEY` = your key |

## Configuration

| Variable | Required | Default | Description |
|----------|:--------:|---------|-------------|
| `PUBLICDATA_API_KEY` | **Yes** | — | data.go.kr API key (인증키) |
| `PUBLICDATA_TIMEOUT_SECONDS` | — | 30 | Per-request timeout |
| `PUBLICDATA_MAX_RESPONSE_LENGTH` | — | 50000 | Max response body length |

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## See Also — FieldCure MCP Ecosystem

| Package | Description |
|---------|-------------|
| **[FieldCure.Mcp.PublicData.Kr](https://github.com/fieldcure/fieldcure-mcp-publicdata)** | Korean public data API gateway (data.go.kr) |
| [FieldCure.Mcp.Essentials](https://github.com/fieldcure/fieldcure-mcp-essentials) | Web search, HTTP, shell, JavaScript, file I/O, memory (10 tools) |
| [FieldCure.Mcp.Outbox](https://github.com/fieldcure/fieldcure-mcp-outbox) | Multi-channel messaging: SMTP, Slack, Telegram, KakaoTalk |
| [FieldCure.Mcp.Filesystem](https://github.com/fieldcure/fieldcure-mcp-filesystem) | File management with document parsing (DOCX, HWPX, XLSX, PDF) |
| [FieldCure.Mcp.Rag](https://github.com/fieldcure/fieldcure-mcp-rag) | Semantic search with bilingual keyword generation |
| [AssistStudio](https://github.com/fieldcure/fieldcure-assiststudio-runner) | Multi-provider AI workspace — use all MCP servers together |

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-publicdata)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-publicdata/blob/main/LICENSE)
- [MCP Specification](https://modelcontextprotocol.io)
