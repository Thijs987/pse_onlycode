using System;

/*
 * Result<T> is a lightweight service result contract used by business logic methods.
 * It allows a service call to return either a successful payload or a typed error,
 * without throwing exceptions for normal validation/authentication failures.
 *
 * Example:
 *   var result = await authService.Login(email, password);
 *   if (result.IsSuccess) {
 *       var user = result.Value;
 *   } else {
 *       var error = result.Error;
 *   }
 */

namespace Application.Results;

public class Result
{
    /// <summary>
    /// True when the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; }
    /// <summary>
    /// True when the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;
    /// <summary>
    /// Contains the failure details when IsFailure is true.
    /// </summary>
    public ServiceError? Error { get; }

    protected Result(bool isSuccess, ServiceError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(ServiceError error) => new(false, error);
}

public sealed class Result<T> : Result
{
    /// <summary>
    /// The successful payload returned by the operation.
    /// </summary>
    public T Value { get; }

    private Result(T value)
        : base(true, null)
    {
        Value = value;
    }

    private Result(ServiceError error)
        : base(false, error)
    {
        Value = default!;
    }

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(ServiceError error) => new(error);
}
