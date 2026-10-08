using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Releaser.Domain.Common;
using Releaser.Domain.Rollouts;

namespace Releaser.Server.Infrastructure.Persistence;

/// <summary>EF Core converters between domain value objects and database columns.</summary>
internal static class Conversions
{
    public static readonly ValueConverter<AppId, Guid> AppId = new(id => id.Value, value => new AppId(value));
    public static readonly ValueConverter<ReleaseId, Guid> ReleaseId = new(id => id.Value, value => new ReleaseId(value));
    public static readonly ValueConverter<DeploymentId, Guid> DeploymentId = new(id => id.Value, value => new DeploymentId(value));
    public static readonly ValueConverter<AudienceId, Guid> AudienceId = new(id => id.Value, value => new AudienceId(value));
    public static readonly ValueConverter<PolicyId, Guid> PolicyId = new(id => id.Value, value => new PolicyId(value));
    public static readonly ValueConverter<ApplicationKey, string> ApplicationKey = new(key => key.Value, value => Domain.Common.ApplicationKey.From(value));
    public static readonly ValueConverter<ChannelKey, string> ChannelKey = new(key => key.Value, value => Domain.Common.ChannelKey.From(value));
    public static readonly ValueConverter<SemanticVersion, string> Version = new(version => version.Value, value => SemanticVersion.Parse(value));
    public static readonly ValueConverter<RolloutPercentage, int> Percentage = new(percentage => percentage.BasisPoints, value => RolloutPercentage.From(value / 100m));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Stores an immutable value as jsonb. Entities must <b>replace</b> such values, never mutate them: the snapshot is the
    /// same instance, so in-place changes would go unnoticed. Use <see cref="HasJsonListConversion{TElement}"/> for lists
    /// that entities change in place.
    /// </summary>
    public static void HasJsonConversion<T>(this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                json => JsonSerializer.Deserialize<T>(json, JsonOptions)!,
                new ValueComparer<T>(
                    (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
                    value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
                    value => value))
            .HasColumnType("jsonb");
    }

    /// <summary>
    /// Stores a list that the entity changes in place (e.g. <c>Release.Channels</c>) as jsonb. Change detection is
    /// element-wise against a copied snapshot, so additions and removals are saved without JSON round-trips.
    /// </summary>
    public static void HasJsonListConversion<TElement>(this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<IReadOnlyList<TElement>> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                json => JsonSerializer.Deserialize<List<TElement>>(json, JsonOptions)!,
                new ValueComparer<IReadOnlyList<TElement>>(
                    (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
                    value => value.Aggregate(0, (hash, element) => HashCode.Combine(hash, element)),
                    value => value.ToList()))
            .HasColumnType("jsonb");
    }
}
