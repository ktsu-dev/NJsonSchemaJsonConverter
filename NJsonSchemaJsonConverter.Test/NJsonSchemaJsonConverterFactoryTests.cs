// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ktsu.NJsonSchemaJsonConverter.Test;

using System.Text.Json;
using NJsonSchema;

[TestClass]
public class NJsonSchemaJsonConverterFactoryTests
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		Converters = { new NJsonSchemaJsonConverterFactory() },
	};

	private readonly NJsonSchemaJsonConverterFactory factory = new();

	[TestMethod]
	public void CanConvertShouldReturnTrueForJsonSchemaType()
	{
		bool result = factory.CanConvert(typeof(JsonSchema));
		Assert.IsTrue(result);
	}

	[TestMethod]
	public void CanConvertShouldReturnFalseForStringType()
	{
		bool result = factory.CanConvert(typeof(string));
		Assert.IsFalse(result);
	}

	[TestMethod]
	public void CanConvertShouldReturnFalseForIntType()
	{
		bool result = factory.CanConvert(typeof(int));
		Assert.IsFalse(result);
	}

	[TestMethod]
	public void CanConvertShouldReturnFalseForObjectType()
	{
		bool result = factory.CanConvert(typeof(object));
		Assert.IsFalse(result);
	}

	[TestMethod]
	public void SerializeShouldWriteValidJsonForSimpleSchema()
	{
		// Arrange
		JsonSchema schema = new()
		{
			Type = JsonObjectType.String,
		};

		// Act
		string json = JsonSerializer.Serialize(schema, SerializerOptions);

		// Assert - should be valid JSON containing the type
		Assert.IsNotNull(json);
		using JsonDocument doc = JsonDocument.Parse(json);
		Assert.AreEqual("string", doc.RootElement.GetProperty("type").GetString());
	}

	[TestMethod]
	public void SerializeShouldWriteValidJsonForSchemaWithProperties()
	{
		// Arrange
		JsonSchema schema = new()
		{
			Type = JsonObjectType.Object,
		};
		schema.Properties["name"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.String,
		};
		schema.Properties["age"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.Integer,
		};

		// Act
		string json = JsonSerializer.Serialize(schema, SerializerOptions);

		// Assert - should be valid JSON with properties
		Assert.IsNotNull(json);
		using JsonDocument doc = JsonDocument.Parse(json);
		Assert.IsTrue(doc.RootElement.GetProperty("properties").TryGetProperty("name", out _));
		Assert.IsTrue(doc.RootElement.GetProperty("properties").TryGetProperty("age", out _));
	}

	[TestMethod]
	public void DeserializeShouldParseSchemaFromStringToken()
	{
		// Arrange - the Read method expects a JSON string containing schema JSON
		string schemaJson = "{\"type\":\"string\"}";
		string json = JsonSerializer.Serialize(schemaJson);

		// Act
		JsonSchema? deserialized = JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual(JsonObjectType.String, deserialized.Type);
	}

	[TestMethod]
	public void DeserializeShouldParseSchemaWithPropertiesFromStringToken()
	{
		// Arrange
		string schemaJson = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"age\":{\"type\":\"integer\"}}}";
		string json = JsonSerializer.Serialize(schemaJson);

		// Act
		JsonSchema? deserialized = JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual(JsonObjectType.Object, deserialized.Type);
		Assert.AreEqual(2, deserialized.Properties.Count);
		Assert.IsTrue(deserialized.Properties.ContainsKey("name"));
		Assert.IsTrue(deserialized.Properties.ContainsKey("age"));
	}

	[TestMethod]
	public void DeserializeShouldThrowJsonExceptionForNonStringToken()
	{
		// Arrange - a JSON number, not a string
		string json = "42";

		// Act & Assert
		_ = Assert.ThrowsExactly<JsonException>(() =>
			JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions));
	}
}
