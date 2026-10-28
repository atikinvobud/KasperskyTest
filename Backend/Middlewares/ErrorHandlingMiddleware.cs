
using System.Text.Json;
using Backend.Share;

namespace Backend.Middlewares;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Запрос {Method} {Path} отменён клиентом.",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Необработанная ошибка при запросе {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                isSuccess = false,
                value = (object?)null,
                errorMessage =
                    ErrorService.GetMessage(ErrorCode.InternalServerError),
                code = nameof(ErrorCode.InternalServerError)
            };

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                cancellationToken: context.RequestAborted);
        }
    }
}
