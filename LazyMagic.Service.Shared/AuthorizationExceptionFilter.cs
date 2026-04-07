using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace LazyMagic.Service.Shared;

/// <summary>
/// Exception filter that converts AuthorizationException instances to appropriate HTTP responses.
/// This serves as a safety net for any authorization exceptions that escape the controller try-catch blocks.
/// </summary>
public class AuthorizationExceptionFilter : IExceptionFilter
{
    private readonly ILogger<AuthorizationExceptionFilter>? _logger;

    public AuthorizationExceptionFilter(ILogger<AuthorizationExceptionFilter>? logger = null)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is AuthorizationException authEx)
        {
            LogAuthorizationException(authEx);

            var errorResponse = CreateErrorResponse(authEx);

            context.Result = new ObjectResult(errorResponse)
            {
                StatusCode = authEx.StatusCode
            };

            context.ExceptionHandled = true;
        }
    }

    private void LogAuthorizationException(AuthorizationException authEx)
    {
        switch (authEx)
        {
            case PermissionDeniedException permEx:
                _logger?.LogWarning(
                    "Permission denied for user '{UserName}' on endpoint '{Endpoint}'. " +
                    "User permissions: [{UserPermissions}], Required: [{RequiredPermissions}]",
                    permEx.UserName,
                    permEx.Endpoint,
                    string.Join(", ", permEx.UserPermissions),
                    string.Join(", ", permEx.RequiredPermissions));
                break;

            case AuthenticationRequiredException:
                _logger?.LogInformation("Authentication required: {Message}", authEx.Message);
                break;

            case AuthorizationConfigurationException:
                _logger?.LogError(authEx, "Authorization configuration error: {Message}", authEx.Message);
                break;

            case AuthorizationBadRequestException:
                _logger?.LogWarning("Authorization bad request: {Message}", authEx.Message);
                break;

            default:
                _logger?.LogWarning(authEx, "Authorization exception: {Message}", authEx.Message);
                break;
        }
    }

    private static object CreateErrorResponse(AuthorizationException authEx)
    {
        var baseResponse = new Dictionary<string, object>
        {
            ["error"] = authEx.ErrorCode,
            ["message"] = authEx.Message
        };

        // Add extra details for PermissionDeniedException (useful for debugging)
        if (authEx is PermissionDeniedException permEx)
        {
            baseResponse["endpoint"] = permEx.Endpoint;
            baseResponse["userPermissions"] = permEx.UserPermissions;
            baseResponse["requiredPermissions"] = permEx.RequiredPermissions;
        }

        return baseResponse;
    }
}

/// <summary>
/// Extension methods for registering the AuthorizationExceptionFilter.
/// </summary>
public static class AuthorizationExceptionFilterExtensions
{
    /// <summary>
    /// Adds the AuthorizationExceptionFilter to the MVC options.
    /// Call this when configuring services: services.AddControllers(options => options.AddAuthorizationExceptionFilter());
    /// </summary>
    public static MvcOptions AddAuthorizationExceptionFilter(this MvcOptions options)
    {
        options.Filters.Add<AuthorizationExceptionFilter>();
        return options;
    }
}
