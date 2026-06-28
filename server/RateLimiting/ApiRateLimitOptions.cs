namespace Poseidon.Server.RateLimiting;

public sealed class ApiRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; init; } = 100;
    public int WindowSeconds { get; init; } = 60;
}
