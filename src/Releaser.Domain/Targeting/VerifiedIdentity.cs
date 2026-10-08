namespace Releaser.Domain.Targeting;

/// <summary>Identity claims that were authenticated (e.g. via a publisher-signed token). Never built from unverified client input.</summary>
public sealed record VerifiedIdentity(
    string? UserId,
    string? CustomerId,
    IReadOnlySet<string> Groups,
    IReadOnlyDictionary<string, string> Attributes)
{
    public static VerifiedIdentity Anonymous { get; } =
        new(null, null, new HashSet<string>(), new Dictionary<string, string>());
}
