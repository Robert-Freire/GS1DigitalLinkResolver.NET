using GS1Resolver.Shared.Exceptions;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Text.Json;

namespace WebResolverService.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
        catch (ResolverException ex)
        {
            _logger.LogWarning(ex, "Resolver exception occurred: {Message}", ex.Message);
            await HandleResolverExceptionAsync(context, ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB exception occurred: {Message}", ex.Message);
            await HandleCosmosExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private static Task HandleResolverExceptionAsync(HttpContext context, ResolverException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception.StatusCode;

        var response = new
        {
            type = $"https://tools.ietf.org/html/rfc7231#section-6.5.{exception.StatusCode}",
            title = "Resolver Error",
            status = exception.StatusCode,
            detail = exception.Message
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static Task HandleCosmosExceptionAsync(HttpContext context, CosmosException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)exception.StatusCode;

        var response = new
        {
            type = $"https://tools.ietf.org/html/rfc7231#section-6.5.{(int)exception.StatusCode}",
            title = "Database Error",
            status = (int)exception.StatusCode,
            detail = exception.Message
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static Task HandleGenericExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "Internal Server Error",
            status = (int)HttpStatusCode.InternalServerError,
            detail = "An unexpected error occurred. Please try again later."
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
