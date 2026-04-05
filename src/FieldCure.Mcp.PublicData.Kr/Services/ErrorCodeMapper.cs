namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Maps data.go.kr result codes to human-readable Korean guidance messages that an LLM
/// can relay to the user without additional processing.
/// </summary>
static class ErrorCodeMapper
{
    /// <summary>
    /// Returns a guidance message for the given <paramref name="resultCode"/>,
    /// or <c>null</c> when the code indicates success (<c>"00"</c>) or is unknown.
    /// </summary>
    /// <param name="resultCode">
    /// The <c>resultCode</c> value extracted from the data.go.kr XML/JSON response header.
    /// </param>
    /// <param name="serviceId">
    /// Optional service identifier used to build the application URL for ACCESS_DENIED errors.
    /// </param>
    /// <returns>A Korean-language message the LLM can present directly, or <c>null</c>.</returns>
    public static string? GetMessage(string? resultCode, string? serviceId = null)
    {
        return resultCode switch
        {
            null or "00" or "0" => null, // success
            "1" => "해당 서비스를 찾을 수 없습니다. serviceId를 확인해주세요.",
            "4" => "HTTP 요청 메서드가 잘못되었습니다.",
            "12" => "이 API가 존재하지 않습니다. discover_api로 다시 검색해보세요.",
            "20" => $"이 API에 대한 활용신청이 필요합니다. " +
                    $"https://www.data.go.kr/data/{serviceId ?? "unknown"}/openapi.do 에서 신청할 수 있습니다.",
            "22" => "API 키가 등록되지 않았습니다. data.go.kr 마이페이지에서 키를 확인하세요.",
            "30" => "일일 호출 한도를 초과했습니다. 내일 다시 시도해주세요.",
            "31" => "이 IP가 등록되지 않았습니다. data.go.kr에서 IP를 등록해야 합니다.",
            _ => $"알 수 없는 에러 코드: {resultCode}",
        };
    }
}
