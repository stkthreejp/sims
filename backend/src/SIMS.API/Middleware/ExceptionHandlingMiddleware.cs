using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SIMS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            if (FindPostgresException(ex) is not null)
                _logger.LogWarning(ex, "Database constraint rejected the request: {Message}", ex.Message);
            else
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static PostgresException? FindPostgresException(Exception exception) => exception switch
    {
        PostgresException pg => pg,
        DbUpdateException { InnerException: not null } db => FindPostgresException(db.InnerException!),
        _ => null,
    };

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (status, title, detail) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Access denied."),
            ArgumentException ex => (HttpStatusCode.BadRequest, "Bad Request", ex.ParamName != null
                ? $"Invalid value for parameter '{ex.ParamName}'."
                : "Invalid argument."),
            _ when FindPostgresException(exception) is { SqlState: "P0001" } pg =>
                (HttpStatusCode.Conflict, "Configuration rule violation", pg.MessageText),
            _ when FindPostgresException(exception) is { SqlState: "23505" } =>
                (HttpStatusCode.Conflict, "Duplicate record", "A record with the same unique value already exists (it may be a previously deleted record)."),
            _ when FindPostgresException(exception) is { SqlState: "23503" } =>
                (HttpStatusCode.Conflict, "Invalid reference", "A referenced record does not exist, or this record is still referenced by other records."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)status;

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)status}",
            title,
            status = (int)status,
            detail,
            instance = context.Request.Path.ToString()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
