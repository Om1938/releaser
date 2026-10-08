using System.Text.Json;
using System.Text.Json.Serialization;

namespace Releaser.Domain.Common;

/// <summary>A value object represented by a single validated string.</summary>
public interface IStringValue<TSelf> where TSelf : IStringValue<TSelf>
{
    string Value { get; }
    static abstract TSelf From(string value);
}

/// <summary>Serializes string value objects as plain JSON strings, re-validating on read.</summary>
public sealed class StringValueJsonConverter<T> : JsonConverter<T> where T : IStringValue<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        T.From(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
