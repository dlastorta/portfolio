using MediatR;
using Microsoft.Extensions.Logging;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Behaviors;

/// <summary>
/// Outermost behavior: logs every request on the way in and the outcome on the way out.
/// Runs first, so it wraps validation and transactions.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next();

        if (response is Result { IsFailure: true } result)
        {
            logger.LogWarning(
                "{RequestName} failed: {ErrorType} - {ErrorMessage}",
                requestName,
                result.Error.Type,
                result.Error.Message);
        }
        else
        {
            logger.LogInformation("Handled {RequestName}", requestName);
        }

        return response;
    }
}
