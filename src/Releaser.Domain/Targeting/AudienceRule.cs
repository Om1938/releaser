using System.Text.Json.Serialization;
using Releaser.Domain.Common;

namespace Releaser.Domain.Targeting;

/// <summary>How specific an audience rule is. Higher wins when deployments overlap (ADR 0006).</summary>
public enum AudienceSpecificity
{
    Everyone = 0,
    Attribute = 1,
    Customer = 2,
    Group = 3,
    User = 4,
    Installation = 5,
}

/// <summary>One matching condition of an audience.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EveryoneRule), "everyone")]
[JsonDerivedType(typeof(InstallationRule), "installation")]
[JsonDerivedType(typeof(UserRule), "user")]
[JsonDerivedType(typeof(GroupRule), "group")]
[JsonDerivedType(typeof(CustomerRule), "customer")]
[JsonDerivedType(typeof(AttributeRule), "attribute")]
[JsonDerivedType(typeof(PlatformRule), "platform")]
[JsonDerivedType(typeof(CurrentVersionRule), "currentVersion")]
public abstract record AudienceRule
{
    [JsonIgnore]
    public abstract AudienceSpecificity Specificity { get; }

    public abstract bool Matches(TargetContext context);
}

public sealed record EveryoneRule : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Everyone;
    public override bool Matches(TargetContext context) => true;
}

public sealed record InstallationRule(IReadOnlyList<string> InstallationIds) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Installation;
    public override bool Matches(TargetContext context) =>
        context.Installation is not null && InstallationIds.Contains(context.Installation.Value, StringComparer.Ordinal);
}

public sealed record UserRule(IReadOnlyList<string> UserIds) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.User;
    public override bool Matches(TargetContext context) =>
        context.Identity.UserId is not null && UserIds.Contains(context.Identity.UserId, StringComparer.Ordinal);
}

public sealed record GroupRule(IReadOnlyList<string> Groups) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Group;
    public override bool Matches(TargetContext context) => Groups.Any(context.Identity.Groups.Contains);
}

public sealed record CustomerRule(IReadOnlyList<string> CustomerIds) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Customer;
    public override bool Matches(TargetContext context) =>
        context.Identity.CustomerId is not null && CustomerIds.Contains(context.Identity.CustomerId, StringComparer.Ordinal);
}

/// <summary>Matches a verified custom attribute against a set of allowed values.</summary>
public sealed record AttributeRule(string Name, IReadOnlyList<string> Values) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Attribute;
    public override bool Matches(TargetContext context) =>
        context.Identity.Attributes.TryGetValue(Name, out var actual) && Values.Contains(actual, StringComparer.Ordinal);
}

public sealed record PlatformRule(IReadOnlyList<PlatformTarget> Platforms) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Attribute;
    public override bool Matches(TargetContext context) => Platforms.Contains(context.Platform);
}

/// <summary>Matches installations whose current version is within [Minimum, MaximumExclusive).</summary>
public sealed record CurrentVersionRule(string? Minimum, string? MaximumExclusive) : AudienceRule
{
    [JsonIgnore]
    public override AudienceSpecificity Specificity => AudienceSpecificity.Attribute;

    public override bool Matches(TargetContext context) =>
        (Minimum is null || context.CurrentVersion >= SemanticVersion.Parse(Minimum))
        && (MaximumExclusive is null || context.CurrentVersion < SemanticVersion.Parse(MaximumExclusive));
}
