using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ValePedagio.Infrastructure.Persistence;

internal static class JsonbPropertyBuilderExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    public static PropertyBuilder<TProperty> HasJsonbConversion<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        var converter = new ValueConverter<TProperty, string?>(
            value => Serialize(value),
            json => Deserialize<TProperty>(json));

        var comparer = new ValueComparer<TProperty>(
            (left, right) => string.Equals(Serialize(left), Serialize(right), StringComparison.Ordinal),
            value => GetHashCodeForValue(value),
            value => Clone(value));

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);
        propertyBuilder.HasColumnType("jsonb");
        return propertyBuilder;
    }

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string? Serialize<TProperty>(TProperty value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
    }

    private static TProperty Deserialize<TProperty>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<TProperty>(json, SerializerOptions)!;
    }

    private static TProperty Clone<TProperty>(TProperty value)
    {
        var json = Serialize(value);
        return json is null ? value! : Deserialize<TProperty>(json);
    }

    private static int GetHashCodeForValue<TProperty>(TProperty value)
    {
        var json = Serialize(value);
        return json is null ? 0 : json.GetHashCode(StringComparison.Ordinal);
    }
}
