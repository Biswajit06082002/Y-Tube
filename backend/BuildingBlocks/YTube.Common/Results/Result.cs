namespace YTube.Common.Results;

public enum ErrorType
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict,
    TooManyRequests,
    Gone,
    Unavailable
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);
    public static Error Gone(string code, string message) => new(code, message, ErrorType.Gone);
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}

/// <summary>
/// Business outcome without exceptions: expected failures (wrong OTP, duplicate email)
/// are values, while exceptions are reserved for things that are actually broken.
/// </summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
