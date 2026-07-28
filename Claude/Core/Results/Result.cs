namespace ClaudeLauncher.Core.Results;

public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Error { get; }

    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && !string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Successful result cannot contain an error.");

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Failed result must contain an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success()
    {
        return new Result(true, null);
    }

    public static Result Failure(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(false, error);
    }

    public static Result<T> Success<T>(T value)
    {
        return Result<T>.Success(value);
    }

    public static Result<T> Failure<T>(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Result<T>.Failure(error);
    }
}