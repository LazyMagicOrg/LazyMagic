namespace LazyMagic.Service.Shared;

/// <summary>
/// Base exception for authorization-related failures.
/// Contains an HTTP status code that should be returned to the client.
/// </summary>
public abstract class AuthorizationException : Exception
{
    /// <summary>
    /// The HTTP status code that should be returned to the client.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// A machine-readable error code for client-side handling.
    /// </summary>
    public string ErrorCode { get; }

    protected AuthorizationException(string message, int statusCode, string errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when authentication is required but not provided (missing or invalid token).
/// Returns HTTP 401 Unauthorized.
/// </summary>
public class AuthenticationRequiredException : AuthorizationException
{
    public AuthenticationRequiredException(string message = "Authentication required")
        : base(message, 401, "authentication_required") { }

    public AuthenticationRequiredException(string message, Exception innerException)
        : base(message, 401, "authentication_required", innerException) { }
}

/// <summary>
/// Thrown when the user is authenticated but lacks permission for the requested operation.
/// Returns HTTP 403 Forbidden.
/// </summary>
public class PermissionDeniedException : AuthorizationException
{
    /// <summary>
    /// The username of the user who was denied access.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// The endpoint/method that was being accessed.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// The permissions the user has.
    /// </summary>
    public IReadOnlyList<string> UserPermissions { get; }

    /// <summary>
    /// The permissions required to access the endpoint.
    /// </summary>
    public IReadOnlyList<string> RequiredPermissions { get; }

    public PermissionDeniedException(
        string userName,
        string endpoint,
        IEnumerable<string> userPermissions,
        IEnumerable<string> requiredPermissions)
        : base(
            $"User '{userName}' does not have permission to access '{endpoint}'",
            403,
            "permission_denied")
    {
        UserName = userName;
        Endpoint = endpoint;
        UserPermissions = userPermissions.ToList().AsReadOnly();
        RequiredPermissions = requiredPermissions.ToList().AsReadOnly();
    }

    public PermissionDeniedException(string userName, string endpoint)
        : base(
            $"User '{userName}' does not have permission to access '{endpoint}'",
            403,
            "permission_denied")
    {
        UserName = userName;
        Endpoint = endpoint;
        UserPermissions = Array.Empty<string>();
        RequiredPermissions = Array.Empty<string>();
    }
}

/// <summary>
/// Thrown when there is a configuration or system error in the authorization system.
/// Returns HTTP 503 Service Unavailable.
/// </summary>
public class AuthorizationConfigurationException : AuthorizationException
{
    public AuthorizationConfigurationException(string message)
        : base(message, 503, "authorization_configuration_error") { }

    public AuthorizationConfigurationException(string message, Exception innerException)
        : base(message, 503, "authorization_configuration_error", innerException) { }
}

/// <summary>
/// Thrown when the request is malformed (e.g., invalid headers, malformed token).
/// Returns HTTP 400 Bad Request.
/// </summary>
public class AuthorizationBadRequestException : AuthorizationException
{
    public AuthorizationBadRequestException(string message)
        : base(message, 400, "bad_request") { }

    public AuthorizationBadRequestException(string message, Exception innerException)
        : base(message, 400, "bad_request", innerException) { }
}
