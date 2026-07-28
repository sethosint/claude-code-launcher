namespace ClaudeLauncher.Services.Credits;

public enum CreditStatus
{
    /// <summary>Provider responded successfully; credits/quota are fine.</summary>
    Available,

    /// <summary>Provider responded with a status that indicates exhausted credits or quota.</summary>
    Exhausted,

    /// <summary>Provider could not be reached or returned an unrelated error; treat as inconclusive.</summary>
    Unknown
}
