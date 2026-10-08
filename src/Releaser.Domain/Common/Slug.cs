using System.Text.RegularExpressions;

namespace Releaser.Domain.Common;

internal static partial class Slug
{
    public static string Validate(string value, string concept) =>
        Pattern().IsMatch(value)
            ? value
            : throw new DomainRuleException($"{concept}.invalid", $"{concept} '{value}' must be 1-64 lowercase letters, digits or single dashes.");

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
