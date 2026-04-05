namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Validates request URLs against an allowed-host whitelist to prevent SSRF attacks.
/// Only <c>http</c> and <c>https</c> schemes targeting pre-approved Korean public-data
/// hosts are permitted.
/// </summary>
static class DomainWhitelist
{
    /// <summary>
    /// Hosts that <see cref="Validate"/> will accept.
    /// </summary>
    static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.odcloud.kr",
        "apis.data.go.kr",
        "api.data.go.kr",
        "openapi.data.go.kr",
        "www.law.go.kr",
        "open.neis.go.kr",
    };

    /// <summary>
    /// Parses <paramref name="url"/> and checks its host against the whitelist.
    /// </summary>
    /// <param name="url">Raw URL string supplied by the caller.</param>
    /// <returns>
    /// A tuple of (<see cref="Uri"/>?, error). When the URL is valid and allowed,
    /// <c>uri</c> is non-null and <c>error</c> is null. Otherwise <c>uri</c> is null
    /// and <c>error</c> contains a human-readable reason.
    /// </returns>
    public static (Uri? uri, string? error) Validate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (null, "URL is required.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return (null, $"Invalid URL: {url}");

        if (uri.Scheme is not ("http" or "https"))
            return (null, $"Only http/https schemes are allowed. Got: {uri.Scheme}");

        if (!AllowedHosts.Contains(uri.Host))
            return (null, $"Host '{uri.Host}' is not in the allowed list. " +
                          $"Allowed: {string.Join(", ", AllowedHosts)}");

        return (uri, null);
    }
}
