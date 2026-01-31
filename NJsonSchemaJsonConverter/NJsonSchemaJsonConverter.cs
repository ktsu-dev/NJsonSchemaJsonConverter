// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.NJsonSchemaJsonConverter;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;

/// <summary>
/// A factory for creating JSON converters for NJsonSchema types.
/// </summary>
public class NJsonSchemaJsonConverterFactory : JsonConverterFactory
{
	/// <summary>
	/// Determines whether the specified type can be converted by this factory.
	/// </summary>
	/// <param name="typeToConvert">The type to check for conversion capability.</param>
	/// <returns>True if the type can be converted; otherwise, false.</returns>
	public override bool CanConvert(Type typeToConvert)
	{
		Ensure.NotNull(typeToConvert);
		return typeToConvert == typeof(JsonSchema) || typeToConvert.IsSubclassOf(typeof(JsonSchema));
	}

	/// <summary>
	/// Creates a JSON converter for the specified type.
	/// </summary>
	/// <param name="typeToConvert">The type to create a converter for.</param>
	/// <param name="options">Options to control the behavior during serialization and deserialization.</param>
	/// <returns>A JSON converter for the specified type.</returns>
	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		Ensure.NotNull(typeToConvert);
		Type converterType = typeof(NJsonSchemaJsonConverter<>).MakeGenericType(typeToConvert);
		return (JsonConverter)Activator.CreateInstance(converterType, BindingFlags.Instance | BindingFlags.Public, binder: null, args: null, culture: null)!;
	}

	/// <summary>
	/// A JSON converter for NJsonSchema types.
	/// </summary>
	/// <typeparam name="T">The type of the object to convert.</typeparam>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection in CreateConverter")]
	private sealed class NJsonSchemaJsonConverter<T> : JsonConverter<T>
	{
		/// <summary>
		/// Reads and converts the JSON to the specified type.
		/// </summary>
		/// <param name="reader">The reader to read the JSON from.</param>
		/// <param name="typeToConvert">The type to convert to.</param>
		/// <param name="options">Options to control the behavior during serialization and deserialization.</param>
		/// <returns>The converted value.</returns>
		public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			Ensure.NotNull(typeToConvert);
			return reader.TokenType == JsonTokenType.String
				? (T)(object)JsonSchema.FromJsonAsync(reader.GetString()!).Result
				: throw new JsonException();
		}

		/// <summary>
		/// Writes the specified value as JSON.
		/// </summary>
		/// <param name="writer">The writer to write the JSON to.</param>
		/// <param name="value">The value to write.</param>
		/// <param name="options">Options to control the behavior during serialization and deserialization.</param>
		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
#pragma warning disable KTSU0003
			ArgumentNullException.ThrowIfNull(value);
#pragma warning restore KTSU0003
			Ensure.NotNull(writer);
			writer.WriteRawValue(((JsonSchema)(object)value).ToJson());
		}
	}
}
