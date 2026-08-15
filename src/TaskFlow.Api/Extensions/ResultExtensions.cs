using TaskFlow.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Extensions;

/// <summary>Maps a service-layer Result/Result&lt;T&gt; to the right HTTP status via ErrorType — RFC 9457 ProblemDetails either way, never a bare string.</summary>
public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.Succeeded)
        {
            return controller.NoContent();
        }

        return Problem(result, controller);
    }

    public static ActionResult<T> ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.Succeeded)
        {
            return controller.Ok(result.Value);
        }

        return Problem(result, controller);
    }

    private static ObjectResult Problem(Result result, ControllerBase controller)
    {
        var status = result.ErrorType switch
        {
            ResultErrorType.NotFound => StatusCodes.Status404NotFound,
            ResultErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return controller.Problem(statusCode: status, title: result.Error);
    }
}
