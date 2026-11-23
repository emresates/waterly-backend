using Waterly.Api.DTOs.Responses;
using Waterly.Api.Exceptions;

namespace Waterly.Api.Middleware;

// Catches every exception thrown while handling a request and writes a consistent ApiResponse.
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorAsync(context, ex.StatusCode, ex.ErrorCode, ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the connection; there is nobody to answer.
            logger.LogInformation("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalServerError,
                "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
        }
    }

    public static async Task WriteErrorAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(statusCode, errorCode, message));
    }
}
