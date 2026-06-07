/*
 * ServiceError and ServiceErrorCode define a reusable set of business-level
 * failure types for service methods. They are intended for normal application
 * errors such as validation problems, duplicate entries, authentication issues,
 * rate limiting, or account lockouts.
 *
 * This allows callers to distinguish:
 * - a real server/internal failure (InternalError)
 * - a validation or business failure that should be reported to the client
 *   with an appropriate HTTP status code.
 */

namespace Application.Results;

public enum ServiceErrorCode
{
    InvalidInput,
    DuplicateEmail,
    DuplicateUsername,
    InvalidCredentials,
    AccountLocked,
    EmailNotVerified,
    RateLimited,
    TooManyAttempts,
    InternalError,
    NotFound,
    InvalidOperation
}

public sealed class ServiceError
{
    /// <summary>
    /// The category of service failure.
    /// </summary>
    public ServiceErrorCode Code { get; }
    /// <summary>
    /// A human-readable description of the failure.
    /// </summary>
    public string Message { get; }

    public ServiceError(ServiceErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }
}
