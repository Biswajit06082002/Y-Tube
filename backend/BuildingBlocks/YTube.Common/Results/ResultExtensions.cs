using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YTube.Common.Responses;

namespace YTube.Common.Results;

public static class ResultExtensions
{
    /// <summary>Maps a business Result to the right HTTP status wrapped in ApiResponse.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, string? successMessage = null)
    {
        if (result.IsSuccess)
            return new OkObjectResult(ApiResponse<T>.Ok(result.Value!, successMessage));

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.Gone => StatusCodes.Status410Gone,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(ApiResponse<T>.Fail(error.Code, error.Message)) { StatusCode = status };
    }
}
