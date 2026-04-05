using System.Reflection;
using System.Text;
using FieldCure.Mcp.PublicData.Kr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Register EUC-KR and other legacy encodings used by some government APIs.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Resolve API key: CLI arg > env var (required)
var apiKey = ResolveArg(args, "--api-key", "PUBLICDATA_API_KEY")
    ?? throw new InvalidOperationException(
        "PUBLICDATA_API_KEY is required. " +
        "Set it as an environment variable or pass --api-key <key>.");

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
    .AddSingleton(new PublicDataHttpClient(apiKey, timeoutSeconds, maxResponseLength))
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-publicdata-kr",
            Version = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0",
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
