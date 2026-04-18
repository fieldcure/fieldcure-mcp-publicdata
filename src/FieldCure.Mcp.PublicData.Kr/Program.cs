using System.Reflection;
using System.Text;
using FieldCure.Mcp.PublicData.Kr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Register EUC-KR and other legacy encodings used by some government APIs.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// ADR-001: API key resolution is lazy. The server starts successfully even without
// a key configured. Tools resolve the key on first call via ApiKeyResolver, which
// checks: CLI arg (--api-key) → env var (DATA_GO_KR_API_KEY, legacy PUBLICDATA_API_KEY)
// → MCP Elicitation → soft-fail. Callers never block tools/list on credentials.
var cliKey = ResolveCliArg(args, "--api-key");

var timeoutSeconds = int.TryParse(
    ResolveArg(args, "--timeout", "PUBLICDATA_TIMEOUT_SECONDS"), out var t) ? t : 30;

var maxResponseLength = int.TryParse(
    ResolveArg(args, "--max-response-length", "PUBLICDATA_MAX_RESPONSE_LENGTH"), out var m) ? m : 50_000;

var builder = Host.CreateApplicationBuilder(Array.Empty<string>());

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton(new ApiKeyResolver(cliKey))
    .AddSingleton(new PublicDataHttpClient(timeoutSeconds, maxResponseLength))
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-publicdata-kr",
            Title = "FieldCure PublicData.Kr",
            Description = "Korean public data gateway — data.go.kr (80,000+ APIs)",
            Version = GetPublicVersion(),
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
return 0;

/// <summary>
/// Resolves a value from CLI args or environment variable.
/// </summary>
static string? ResolveArg(string[] args, string cliFlag, string envVar)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == cliFlag)
            return args[i + 1];
    }

    var env = Environment.GetEnvironmentVariable(envVar);
    return string.IsNullOrWhiteSpace(env) ? null : env;
}

/// <summary>
/// Resolves a CLI-only arg (no env var fallback). Used for the API key: env var
/// lookup is handled by <see cref="ApiKeyResolver"/> at tool invocation time.
/// </summary>
static string? ResolveCliArg(string[] args, string cliFlag)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == cliFlag)
            return args[i + 1];
    }
    return null;
}

/// <summary>
/// Returns the user-facing server version string. Uses
/// <see cref="AssemblyInformationalVersionAttribute"/> as the source so
/// SemVer prerelease suffixes are preserved (e.g. <c>1.0.0-beta</c>), but
/// strips the <c>+&lt;build-metadata&gt;</c> commit-SHA suffix that the
/// .NET SDK auto-appends — that hash is only useful to developers and
/// just adds noise in end-user UIs. The full versioned attribute is still
/// available on the assembly for diagnostic logs and debuggers.
/// </summary>
static string GetPublicVersion()
{
    var info = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;
    if (string.IsNullOrEmpty(info)) return "0.0.0";
    var plus = info.IndexOf('+');
    return plus > 0 ? info[..plus] : info;
}
