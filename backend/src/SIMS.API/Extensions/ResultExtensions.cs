using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Common;

namespace SIMS.API.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToHttpResult<T>(this Result<T> result, ControllerBase ctrl) =>
        result.IsSuccess
            ? ctrl.Ok(result.Value)
            : MapError(result.ErrorCode, result.ErrorMessage, ctrl);

    public static IActionResult ToHttpResult(this Result result, ControllerBase ctrl) =>
        result.IsSuccess
            ? ctrl.NoContent()
            : MapError(result.ErrorCode, result.ErrorMessage, ctrl);

    // Use when success path needs a non-Ok response (CreatedAtAction, NoContent, etc.)
    public static IActionResult? ToHttpErrorResult<T>(this Result<T> result, ControllerBase ctrl) =>
        result.IsSuccess ? null : MapError(result.ErrorCode, result.ErrorMessage, ctrl);

    public static IActionResult? ToHttpErrorResult(this Result result, ControllerBase ctrl) =>
        result.IsSuccess ? null : MapError(result.ErrorCode, result.ErrorMessage, ctrl);

    private static IActionResult MapError(string? errorCode, string? errorMessage, ControllerBase ctrl) =>
        errorCode switch
        {
            "NOT_FOUND" => ctrl.NotFound(new { ErrorCode = errorCode, ErrorMessage = errorMessage }),
            "CONFLICT" or "DUPLICATE" => ctrl.Conflict(new { ErrorCode = errorCode, ErrorMessage = errorMessage }),
            _ => ctrl.BadRequest(new { ErrorCode = errorCode, ErrorMessage = errorMessage })
        };
}
