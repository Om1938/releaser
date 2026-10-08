using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Releaser.Domain.Common;

namespace Releaser.Domain.Targeting;

/// <summary>Opaque, application-generated identifier of one installation (e.g. a UUID stored in userData).</summary>
[JsonConverter(typeof(StringValueJsonConverter<InstallationId>))]
public sealed partial record InstallationId : IStringValue<InstallationId>
{
    private InstallationId(string value) => Value = value;
    public string Value { get; }

    public static InstallationId From(string value) =>
        Pattern().IsMatch(value)
            ? new InstallationId(value)
            : throw new DomainRuleException("installation_id.invalid", "Installation id must be 8-128 characters of letters, digits, '.', '_' or '-'.");

    public static bool TryFrom(string? value, out InstallationId? id)
    {
        id = value is not null && Pattern().IsMatch(value) ? new InstallationId(value) : null;
        return id is not null;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9._-]{8,128}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}
