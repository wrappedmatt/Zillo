using Zillo.Application.Services;

namespace Zillo.Dashboard.Server.Middleware;

/// <summary>
/// Middleware to authenticate terminal API requests using X-Terminal-API-Key header
/// </summary>
public class TerminalAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TerminalAuthMiddleware> _logger;

    public TerminalAuthMiddleware(RequestDelegate next, ILogger<TerminalAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITerminalService terminalService)
    {
        // Read X-Terminal-API-Key header
        if (!context.Request.Headers.TryGetValue("X-Terminal-API-Key", out var apiKeyHeader))
        {
            _logger.LogWarning("Terminal API key missing from request to {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Terminal API key required" });
            return;
        }

        var apiKey = apiKeyHeader.ToString();

        // Validate API key via TerminalService
        var terminal = await terminalService.ValidateApiKeyAsync(apiKey);

        if (terminal == null)
        {
            _logger.LogWarning("Invalid terminal API key attempted for {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid terminal API key" });
            return;
        }

        // Store accountId and terminalId in HttpContext.Items for use by controllers
        context.Items["AccountId"] = terminal.AccountId;
        context.Items["TerminalId"] = terminal.Id;
        context.Items["TerminalLabel"] = terminal.TerminalLabel;

        _logger.LogDebug("Terminal {TerminalId} ({TerminalLabel}) authenticated for account {AccountId}",
            terminal.Id, terminal.TerminalLabel, terminal.AccountId);

        // Update last seen timestamp (fire and forget to not block request)
        _ = Task.Run(async () =>
        {
            try
            {
                await terminalService.UpdateLastSeenAsync(terminal.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update last seen for terminal {TerminalId}", terminal.Id);
            }
        });

        // Continue to next middleware/controller
        await _next(context);
    }
}

/// <summary>
/// Extension method to easily register TerminalAuthMiddleware
/// </summary>
public static class TerminalAuthMiddlewareExtensions
{
    /// <summary>
    /// Adds terminal authentication middleware to the application pipeline.
    /// Use this on specific routes/endpoints that require terminal authentication.
    /// </summary>
    public static IApplicationBuilder UseTerminalAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TerminalAuthMiddleware>();
    }
}
