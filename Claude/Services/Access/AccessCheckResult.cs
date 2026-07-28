namespace ClaudeLauncher.Services.Access;

/// <summary>Outcome of a pre-launch credential probe against a provider.</summary>
public enum AccessState
{
    /// <summary>The probe could not reach a verdict (network error, unexpected status).</summary>
    Unknown,

    /// <summary>The provider accepted the credentials.</summary>
    Ok,

    /// <summary>The provider rejected the credentials (401/403) or they clearly belong to another provider.</summary>
    Invalid,

    /// <summary>Credentials are valid but there is nothing left to spend (402/429).</summary>
    Exhausted
}

/// <summary>
/// Result of a credential probe.
/// <para>
/// <see cref="Summary"/> is the short form shown in the UI chip; <see cref="Hint"/>
/// is the full, actionable sentence shown when a launch is blocked — the whole point
/// of this check is that the user learns what to change instead of meeting a raw
/// <c>{"error":{"message":"Missing Authentication header","code":401}}</c> inside
/// the Claude session.
/// </para>
/// </summary>
public sealed class AccessCheckResult
{
    public required AccessState State { get; init; }

    public required string Summary { get; init; }

    public string? Hint { get; init; }

    public bool IsBlocking => State == AccessState.Invalid;

    public static AccessCheckResult Ok(string summary) =>
        new() { State = AccessState.Ok, Summary = summary };

    public static AccessCheckResult Invalid(string summary, string hint) =>
        new() { State = AccessState.Invalid, Summary = summary, Hint = hint };

    public static AccessCheckResult Exhausted(string summary, string hint) =>
        new() { State = AccessState.Exhausted, Summary = summary, Hint = hint };

    public static AccessCheckResult Unknown(string summary) =>
        new() { State = AccessState.Unknown, Summary = summary };
}
