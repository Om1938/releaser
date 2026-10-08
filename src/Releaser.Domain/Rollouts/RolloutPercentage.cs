using System.Globalization;
using Releaser.Domain.Common;

namespace Releaser.Domain.Rollouts;

/// <summary>Share of an audience eligible to be offered a release (not adoption). 0.00-100.00, two decimals.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(RolloutPercentageJsonConverter))]
public readonly record struct RolloutPercentage
{
    public const int BasisPointsScale = 10_000;

    private RolloutPercentage(int basisPoints) => BasisPoints = basisPoints;

    /// <summary>Hundredths of a percent: 2000 = 20.00%.</summary>
    public int BasisPoints { get; }
    public decimal Value => BasisPoints / 100m;
    public bool IsFull => BasisPoints == BasisPointsScale;

    public static RolloutPercentage Full { get; } = new(BasisPointsScale);

    public static RolloutPercentage From(decimal percent)
    {
        if (percent is < 0m or > 100m || decimal.Round(percent, 2) != percent)
        {
            throw new DomainRuleException("rollout.percentage_invalid", "Rollout percentage must be between 0 and 100 with at most two decimals.");
        }
        return new RolloutPercentage((int)(percent * 100m));
    }

    public override string ToString() => Value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
