namespace YTube.Common.Responses;

/// <summary>
/// Standard envelope every service returns, so the frontend handles responses one way.
/// (Successor of CommonReturn&lt;T&gt; from the old project.)
/// </summary>
public sealed record ApiResponse<T>(bool Success, T? Data, string? Message = null, string? ErrorCode = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);

    public static ApiResponse<T> Fail(string errorCode, string message) => new(false, default, message, errorCode);
}
