# Release Notes

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
