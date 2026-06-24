using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Messaging;

/// <summary>Non-generic marker so behaviors can detect commands at runtime.</summary>
public interface ICommandBase
{
}

/// <summary>A command changes state and returns a <see cref="Result{TResponse}"/>.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase
{
}
