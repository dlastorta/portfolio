using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Messaging;

/// <summary>A query reads state and returns a <see cref="Result{TResponse}"/>. It never mutates.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
