using MediatR;
using ModularMonolith.Application.Common.Messaging;
using ModularMonolith.Domain.Abstractions;

namespace ModularMonolith.Application.Common.Behaviors;

/// <summary>
/// Innermost behavior: wraps commands in a database transaction. Queries don't mutate
/// state, so they skip the transaction entirely — opening one per read would be waste.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommandBase)
        {
            return await next();
        }

        return await unitOfWork.ExecuteInTransactionAsync(() => next(), cancellationToken);
    }
}
