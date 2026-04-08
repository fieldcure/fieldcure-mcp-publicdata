# FieldCure MCP PublicData.Kr

[![NuGet](https://img.shields.io/nuget/v/FieldCure.Mcp.PublicData.Kr)](https://www.nuget.org/packages/FieldCure.Mcp.PublicData.Kr)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/fieldcure/fieldcure-mcp-publicdata/blob/main/LICENSE)

Korean public data API gateway. A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that lets any MCP client discover, inspect, and call 80,000+ APIs on [data.go.kr](https://www.data.go.kr) — weather, real estate, business registration, air quality, transit, and more. Built with C# and the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- **3 tools** — search APIs by keyword, inspect parameters/response fields, call any data.go.kr API
- **Automatic serviceKey injection** — the API key is added to every request; never leaked to the LLM
- **XML → JSON normalization** — strips the `response/header/body/items` wrapper, returns clean JSON
- **Error code mapping** — translates data.go.kr error codes into Korean guidance messages the LLM can relay directly
- **SSRF protection** — domain whitelist limits calls to approved government hosts
- **EUC-KR support** — legacy encoding from older government APIs is auto-detected and converted
- **Stateless** — no cache, no database, no local files; every call is independent
- **Stdio transport** — standard MCP subprocess model via JSON-RPC over stdin/stdout

## Installation

### dotnet tool (recommended)

```bash
dotnet tool install -g FieldCure.Mcp.PublicData.Kr
```

After installation, the `fieldcure-mcp-publicdata-kr` command is available globally.

### From source

```bash
git clone https://github.com/fieldcure/fieldcure-mcp-publicdata.git
cd fieldcure-mcp-publicdata
dotnet build
```

## Prerequisites

1. Sign up at [data.go.kr](https://www.data.go.kr) and get your API key
   (data.go.kr 회원가입 후 인증키 발급)
2. **Subscribe to [목록조회서비스](https://www.data.go.kr/data/15077093/openapi.do)** (Required)
   — `discover_api` and `describe_api` depend on this API
   (discover_api, describe_api 도구가 이 API를 사용합니다)
3. Subscribe to each individual API you want to query
   (조회하려는 개별 API도 각각 활용신청 필요)

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Configuration

### Claude Desktop

Add to `claude_desktop_config.json`:

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

Add to `.vscode/mcp.json`:

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

### From source (without dotnet tool)

```json
{
  "mcpServers": {
    "publicdata-kr": {
      "command": "dotnet",
      "args": [
        "run",
        "--project", "C:\\path\\to\\fieldcure-mcp-publicdata\\src\\FieldCure.Mcp.PublicData.Kr"
      ],
      "env": {
        "PUBLICDATA_API_KEY": "YOUR_DATA_GO_KR_API_KEY"
      }
    }
  }
}
```

### AssistStudio

Settings > MCP Servers > **Add Server**:

| Field | Value |
|-------|-------|
| **Name** | `PublicData.Kr` |
| **Command** | `fieldcure-mcp-publicdata-kr` |
| **Arguments** | *(empty)* |
| **Environment** | `PUBLICDATA_API_KEY` = your data.go.kr API key |
| **Description** | *(auto-filled on first connection)* |

## Tools

| Tool | Description |
|------|-------------|
| `discover_api` | Search data.go.kr APIs by keyword — returns names, providers, endpoint URLs |
| `describe_api` | Get operations, request parameters, and response fields for a specific API |
| `call_api` | Call any data.go.kr API with automatic serviceKey injection and response normalization |

### Workflow

```
1. discover_api("미세먼지")
   → { serviceId: "15073861", serviceName: "한국환경공단_에어코리아_대기오염정보", ... }

2. describe_api("15073861")
   → { operations: [{ name: "getMsrstnAcctoRltmMesureDnsty", url: "...", requestParameters: [...] }] }

3. call_api(url: "http://apis.data.go.kr/B552584/ArpltnInforInqireSvc/getMsrstnAcctoRltmMesureDnsty",
            params: '{"stationName": "종로구", "dataTerm": "DAILY", "returnType": "json"}')
   → { totalCount: 24, items: [{ stationName: "종로구", pm10Value: "45", ... }] }
```

### `discover_api`

Search Korean public data APIs on data.go.kr by keyword. Results are deduplicated by service — each API appears once even if it has multiple operations.

| Parameter | Type | Required | Description |
|-----------|------|:--------:|-------------|
| `query` | string | Yes | Search keyword (e.g., `미세먼지`, `부동산`, `사업자`) |
| `page` | int | — | Page number (default: 1) |
| `pageSize` | int | — | Results per page (default: 10, max: 50) |

### `describe_api`

Get the request parameters and response fields of a specific API. Use the `serviceId` from `discover_api` results.

| Parameter | Type | Required | Description |
|-----------|------|:--------:|-------------|
| `serviceId` | string | Yes | Service ID (`list_id`) from `discover_api` |

### `call_api`

Call a Korean public data API. The serviceKey is automatically injected — never pass it yourself. If the call fails with ACCESS_DENIED, the user needs to apply for access to that specific API at data.go.kr.

| Parameter | Type | Required | Description |
|-----------|------|:--------:|-------------|
| `url` | string | Yes | Full endpoint URL from `describe_api` results |
| `params` | string | — | Query parameters as JSON object (e.g., `{"stationName": "종로구"}`) |
| `maxResults` | int | — | Max items to return (default: 20, prevents context overflow) |

## Error Code Mapping

When a data.go.kr API returns an error, the server translates it into a Korean guidance message the LLM can relay directly:

| Code | Meaning | LLM receives |
|------|---------|--------------|
| 12 | NO_OPENAPI_SERVICE | 이 API가 존재하지 않습니다. discover_api로 다시 검색해보세요. |
| 20 | ACCESS_DENIED | 이 API에 대한 활용신청이 필요합니다. (포털 링크 포함) |
| 22 | KEY_NOT_REGISTERED | API 키가 등록되지 않았습니다. |
| 30 | TRAFFIC_EXCEEDED | 일일 호출 한도를 초과했습니다. |
| 31 | UNREGISTERED_IP | 이 IP가 등록되지 않았습니다. |

## Environment Variables

| Variable | Required | Default | Description |
|----------|:--------:|---------|-------------|
| `PUBLICDATA_API_KEY` | **Yes** | — | data.go.kr API key (인증키) |
| `PUBLICDATA_TIMEOUT_SECONDS` | — | 30 | Per-request timeout |
| `PUBLICDATA_MAX_RESPONSE_LENGTH` | — | 50000 | Maximum response body length in characters |

CLI args (`--api-key`, `--timeout`, `--max-response-length`) override environment variables.

## Security

- **API key masking** — the serviceKey is replaced with `***` in any error output visible to the LLM
- **Domain whitelist** — `call_api` only allows requests to approved hosts: `api.odcloud.kr`, `apis.data.go.kr`, `api.data.go.kr`, `openapi.data.go.kr`, `www.law.go.kr`, `open.neis.go.kr`
- **Response size limit** — configurable via `PUBLICDATA_MAX_RESPONSE_LENGTH` (default: 50,000 chars)
- **No log leaks** — the API key is never printed to stdout or stderr

## Project Structure

```
src/FieldCure.Mcp.PublicData.Kr/
├── Program.cs                    # MCP server entry point (stdio)
├── Services/
│   ├── PublicDataHttpClient.cs   # HTTP proxy with serviceKey injection
│   ├── DomainWhitelist.cs        # SSRF prevention via host whitelist
│   ├── ResponseNormalizer.cs     # XML→JSON conversion, wrapper removal
│   └── ErrorCodeMapper.cs       # Error code → Korean guidance messages
└── Tools/
    ├── DiscoverApiTool.cs        # discover_api
    ├── DescribeApiTool.cs        # describe_api
    └── CallApiTool.cs            # call_api
```

## Development

```bash
# Build
dotnet build

# Test
dotnet test

# Pack as dotnet tool
dotnet pack src/FieldCure.Mcp.PublicData.Kr -c Release
```

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

## License

[MIT](LICENSE)
