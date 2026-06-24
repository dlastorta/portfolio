using System.Reflection;
using FluentValidation;
using MediatR;
using ModularMonolith.Domain.Common;

namespace ModularMonolith.Application.Common.Behaviors;

/// <summary>
/// Runs all FluentValidation validators registered for the request. If any fail, it
/// short-circuits the pipeline with a failed <see cref="Result{T}"/> — the handler
/// never runs. Validation is a normal outcome, so it returns a Result rather than throwing.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var message = string.Join(" ", failures.Select(f => f.ErrorMessage));
        return CreateFailureResult(Error.Validation(message));
    }

    // Every command/query in this codebase returns Result<TValue>. We build the matching
    // failed Result<TValue> via the open generic Result.Failure<TValue>(Error) factory.
    private static TResponse CreateFailureResult(Error error)
    {
        var responseType = typeof(TResponse);

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];

            var failureFactory = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m is { Name: nameof(Result.Failure), IsGenericMethod: true })
                .MakeGenericMethod(valueType);

            return (TResponse)failureFactory.Invoke(null, [error])!;
        }

        // Defensive: a request whose response isn't Result<T> isn't a shape we support here.
        throw new ValidationException(error.Message);
    }
}
