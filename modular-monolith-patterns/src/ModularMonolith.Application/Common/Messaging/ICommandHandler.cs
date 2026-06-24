using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Messaging;

public interface ICommandHandler<TCommand, TResponse>
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
