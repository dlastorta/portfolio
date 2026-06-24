using ModularMonolith.Domain.Common;

namespace ModularMonolith.WebApi;

/// <summary>
/// Maps an application-layer <see cref="Result{T}"/> onto an HTTP response. This is the
/// only place HTTP status codes meet error types — the application layer never knows
/// anything about HTTP.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null ? onSuccess(result.Value) : Results.Ok(result.Value);
        }

        return result.Error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(ToProblem(result.Error)),
            ErrorType.Validation => Results.BadRequest(ToProblem(result.Error)),
            ErrorType.Conflict => Results.Conflict(ToProblem(result.Error)),
            _ => Results.Json(ToProblem(result.Error), statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static object ToProblem(Error error) => new
    {
        type = error.Type.ToString(),
        code = error.Code,
        message = error.Message
    };
}
