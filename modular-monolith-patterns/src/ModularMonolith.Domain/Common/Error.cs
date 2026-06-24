namespace ModularMonolith.Domain.Common;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Failure = 4
}

/// <summary>
/// A typed error value. Carrying an <see cref="ErrorType"/> lets the presentation
/// layer map a failure to the right transport status (404, 400, 409, ...) without
/// the application layer knowing anything about HTTP.
/// </summary>
public sealed record Error(ErrorType Type, string Code, string Message)
{
    public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);

    public static Error Validation(string message, string code = "validation") =>
        new(ErrorType.Validation, code, message);

    public static Error NotFound(string message, string code = "not_found") =>
        new(ErrorType.NotFound, code, message);

    public static Error Conflict(string message, string code = "conflict") =>
        new(ErrorType.Conflict, code, message);

    public static Error Failure(string message, string code = "failure") =>
        new(ErrorType.Failure, code, message);
}
