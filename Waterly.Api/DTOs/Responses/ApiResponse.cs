using System.Text.Json.Serialization;

namespace Waterly.Api.DTOs.Responses;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    // Only filled for validation errors: field name -> messages.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "İşlem başarılı.", int statusCode = StatusCodes.Status200OK) =>
        new() { Success = true, Message = message, StatusCode = statusCode, Data = data };

    public static ApiResponse<T> Fail(int statusCode, string errorCode, string message, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, StatusCode = statusCode, ErrorCode = errorCode, Errors = errors };
}
