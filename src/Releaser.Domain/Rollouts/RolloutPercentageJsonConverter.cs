using System.Text.Json;
using System.Text.Json.Serialization;

namespace Releaser.Domain.Rollouts;

public sealed class RolloutPercentageJsonConverter : JsonConverter<RolloutPercentage>
{
    public override RolloutPercentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        RolloutPercentage.From(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, RolloutPercentage value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
