using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Releaser.Domain.Common;

/// <summary>Semantic Versioning 2.0.0 version; build metadata is ignored for ordering and equality, as in electron-updater (semver).</summary>
[JsonConverter(typeof(StringValueJsonConverter<SemanticVersion>))]
public sealed partial class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>, IStringValue<SemanticVersion>
{
    private readonly string[] _prerelease;

    private SemanticVersion(long major, long minor, long patch, string[] prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _prerelease = prerelease;
    }

    public long Major { get; }
    public long Minor { get; }
    public long Patch { get; }
    public bool IsPrerelease => _prerelease.Length > 0;
    public string Value => ToString();

    public static SemanticVersion From(string value) => Parse(value);

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new DomainRuleException("version.invalid", $"'{text}' is not a valid semantic version.");

    public static bool TryParse(string? text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        var match = text is null ? null : Pattern().Match(text);
        if (match is not { Success: true })
        {
            return false;
        }
        var prerelease = match.Groups["pre"].Success ? match.Groups["pre"].Value.Split('.') : [];
        version = new SemanticVersion(
            long.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            long.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            long.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }
        var core = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        return core != 0 ? core : ComparePrerelease(_prerelease, other._prerelease);
    }

    private static int ComparePrerelease(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return right.Length.CompareTo(left.Length);
        }
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var result = CompareIdentifier(left[i], right[i]);
            if (result != 0)
            {
                return result;
            }
        }
        return left.Length.CompareTo(right.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumber = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        if (leftIsNumber && rightIsNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }
        if (leftIsNumber != rightIsNumber)
        {
            return leftIsNumber ? -1 : 1;
        }
        return string.CompareOrdinal(left, right);
    }

    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as SemanticVersion);
    public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

    public override string ToString() =>
        IsPrerelease
            ? string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}-{string.Join('.', _prerelease)}")
            : string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);
    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<pre>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
