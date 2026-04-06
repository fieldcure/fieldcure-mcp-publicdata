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

Settings > MCP Servers > **서버 추가**:

| Field | Value |
|-------|-------|
| **서버 이름** | `PublicData.Kr` |
| **Command** | `fieldcure-mcp-publicdata-kr` |
| **Arguments** | *(비워둠)* |
| **환경변수** | `PUBLICDATA_API_KEY` = data.go.kr 인증키 |
| **Description** | *(비워두면 서버 연결 시 자동으로 채워집니다)* |

## Configuration

| Variable | Required | Default | Description |
|----------|:--------:|---------|-------------|
| `PUBLICDATA_API_KEY` | **Yes** | — | data.go.kr API key (인증키) |
| `PUBLICDATA_TIMEOUT_SECONDS` | — | 30 | Per-request timeout |
| `PUBLICDATA_MAX_RESPONSE_LENGTH` | — | 50000 | Max response body length |

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## See Also — AssistStudio Ecosystem

| Package | Description |
|---------|-------------|
| [FieldCure.Mcp.Essentials](https://www.nuget.org/packages/FieldCure.Mcp.Essentials) | HTTP, web search (Bing/Serper/Tavily), shell, JavaScript, file I/O, persistent memory |
| [FieldCure.Mcp.Outbox](https://www.nuget.org/packages/FieldCure.Mcp.Outbox) | Multi-channel messaging — Slack, Telegram, Email (SMTP/Graph), KakaoTalk |
| [FieldCure.Mcp.Filesystem](https://www.nuget.org/packages/FieldCure.Mcp.Filesystem) | Sandboxed file/directory operations with built-in document parsing (DOCX, HWPX, XLSX, PDF) |
| [FieldCure.Mcp.Rag](https://www.nuget.org/packages/FieldCure.Mcp.Rag) | Document search — hybrid BM25 + vector retrieval, multi-KB, incremental indexing |
| [FieldCure.Mcp.PublicData.Kr](https://www.nuget.org/packages/FieldCure.Mcp.PublicData.Kr) | Korean public data gateway — data.go.kr (80,000+ APIs) |
| [FieldCure.AssistStudio.Runner](https://www.nuget.org/packages/FieldCure.AssistStudio.Runner) | Headless LLM task runner with scheduling via Windows Task Scheduler |
| [FieldCure.AssistStudio](https://github.com/fieldcure/fieldcure-assiststudio) | Multi-provider AI workspace for Windows (WinUI 3) |

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-publicdata)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-publicdata/blob/main/LICENSE)
- [MCP Specification](https://modelcontextprotocol.io)
