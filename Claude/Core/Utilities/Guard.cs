namespace ClaudeLauncher.Core.Utilities;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        return value;
    }

    public static string AgainstNullOrWhiteSpace(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "Value cannot be null or whitespace.",
                parameterName);

        return value;
    }

    public static void AgainstFalse(
    bool condition,
    string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void AgainstNegative(
        int value,
        string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}