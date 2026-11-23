namespace Waterly.Api.Exceptions;

// Thrown by services for expected business errors. ExceptionMiddleware turns it into an ApiResponse.
public class AppException(int statusCode, string errorCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;

    public static AppException BadRequest(string errorCode, string message) => new(StatusCodes.Status400BadRequest, errorCode, message);
    public static AppException Unauthorized(string errorCode, string message) => new(StatusCodes.Status401Unauthorized, errorCode, message);
    public static AppException NotFound(string errorCode, string message) => new(StatusCodes.Status404NotFound, errorCode, message);
    public static AppException Conflict(string errorCode, string message) => new(StatusCodes.Status409Conflict, errorCode, message);
}
