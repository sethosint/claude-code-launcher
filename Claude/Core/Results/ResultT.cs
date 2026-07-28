namespace ClaudeLauncher.Core.Results;

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value)
    : base(true, null)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
    }

    private Result(string error)
    : base(false, error)
    {
        ArgumentNullException.ThrowIfNull(error);
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value);
    }

    public new static Result<T> Failure(string error)
    {
        return new Result<T>(error);
    }
}