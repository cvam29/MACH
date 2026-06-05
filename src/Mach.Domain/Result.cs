namespace Mach.Domain;

/// <summary>
/// Describes why an operation failed. <see cref="Code"/> is a stable machine-readable token;
/// <see cref="Message"/> is human-readable.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string message) => new("not_found", message);

    public static Error Validation(string message) => new("validation", message);

    public static Error Conflict(string message) => new("conflict", message);

    public static Error Unexpected(string message) => new("unexpected", message);
}

/// <summary>
/// The outcome of an operation that yields no value: success or failure with an <see cref="Error"/>.
/// </summary>
public readonly record struct Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

/// <summary>
/// The outcome of an operation that yields a <typeparamref name="T"/> on success.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    /// <summary>The success value. Throws when accessed on a failed result.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<T> Success(T value) => new(true, value, Error.None);

    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(Error);
}
