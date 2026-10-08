using System.Text.Json.Serialization;

namespace Releaser.Domain.Common;

/// <summary>Stable public identifier of an application, used in feed URLs.</summary>
[JsonConverter(typeof(StringValueJsonConverter<ApplicationKey>))]
public sealed record ApplicationKey : IStringValue<ApplicationKey>
{
    private ApplicationKey(string value) => Value = value;
    public string Value { get; }
    public static ApplicationKey From(string value) => new(Slug.Validate(value, "application_key"));
    public override string ToString() => Value;
}

/// <summary>Key of a logical release stream such as <c>stable</c> or <c>beta</c>.</summary>
[JsonConverter(typeof(StringValueJsonConverter<ChannelKey>))]
public sealed record ChannelKey : IStringValue<ChannelKey>
{
    private ChannelKey(string value) => Value = value;
    public string Value { get; }
    public static ChannelKey From(string value) => new(Slug.Validate(value, "channel_key"));
    public override string ToString() => Value;
}
