// Copyright (c) 2023-2026 ktsu-dev contributors

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ktsu.NJsonSchemaJsonConverter.Test;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
	public void DeserializeShouldReturnJsonSchemaSubclass()
	{
		Assert.IsTrue(factory.CanConvert(typeof(JsonSchemaProperty)));
		string json = JsonSerializer.Serialize("""{"type":"string","minLength":2}""");

		JsonSchemaProperty? result = JsonSerializer.Deserialize<JsonSchemaProperty>(json, SerializerOptions);

		Assert.IsNotNull(result);
		Assert.AreEqual(JsonObjectType.String, result.Type);
		Assert.AreEqual(2, result.MinLength);
	}

	[TestMethod]
	public void DeserializeShouldReturnJsonSchemaSubclassInContainer()
	{
		string json = JsonSerializer.Serialize(new { Property = """{"type":"integer"}""" });

		PropertyContainer? result = JsonSerializer.Deserialize<PropertyContainer>(json, SerializerOptions);

		Assert.IsNotNull(result);
		Assert.IsNotNull(result.Property);
		Assert.AreEqual(JsonObjectType.Integer, result.Property.Type);
	}

	[TestMethod]
	public void DeserializeShouldNotFetchRemoteReference()
	{
		int port = GetFreeTcpPort();
		string prefix = $"http://127.0.0.1:{port}/";
		using HttpListener listener = new();
		listener.Prefixes.Add(prefix);
		listener.Start();

		int hits = 0;
		_ = Task.Run(async () =>
		{
			while (listener.IsListening)
			{
				HttpListenerContext context;
				try
				{
					context = await listener.GetContextAsync().ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
				{
					return;
				}

				Interlocked.Increment(ref hits);
				byte[] body = Encoding.UTF8.GetBytes("""{"type":"integer"}""");
				context.Response.ContentType = "application/json";
				await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
				context.Response.Close();
			}
		});

		string json = $$"""{"$ref":"{{prefix}}remote.json"}""";

		Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions));
		Assert.AreEqual(0, Volatile.Read(ref hits));
	}

	[TestMethod]
	public void DeserializeShouldStillResolveLocalReference()
	{
		const string json = """{"definitions":{"Item":{"type":"integer"}},"properties":{"item":{"$ref":"#/definitions/Item"}}}""";

		JsonSchema? result = JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions);

		Assert.IsNotNull(result);
		Assert.AreEqual(JsonObjectType.Integer, result.Properties["item"].ActualSchema.Type);
	}

	private static int GetFreeTcpPort()
	{
		using TcpListener probe = new(IPAddress.Loopback, 0);
		probe.Start();
		return ((IPEndPoint)probe.LocalEndpoint).Port;
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

	[TestMethod]
	public void SerializeShouldWriteSchemaInlineInsideContainer()
	{
		// Arrange
		JsonSchema schema = new()
		{
			Type = JsonObjectType.Object,
		};
		schema.Properties["id"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.Integer,
		};

		SchemaContainer container = new()
		{
			Name = "TestSchema",
			Schema = schema,
		};

		// Act
		string json = JsonSerializer.Serialize(container, SerializerOptions);

		// Assert - the container JSON should have the schema written inline
		Assert.IsNotNull(json);
		using JsonDocument doc = JsonDocument.Parse(json);
		Assert.AreEqual("TestSchema", doc.RootElement.GetProperty("Name").GetString());
		Assert.AreEqual(JsonValueKind.Object, doc.RootElement.GetProperty("Schema").ValueKind);
	}

	[TestMethod]
	public void SerializeShouldHandleNullSchemaInContainer()
	{
		// Arrange
		SchemaContainer container = new()
		{
			Name = "NoSchema",
			Schema = null,
		};

		// Act
		string json = JsonSerializer.Serialize(container, SerializerOptions);

		// Assert
		Assert.IsNotNull(json);
		using JsonDocument doc = JsonDocument.Parse(json);
		Assert.AreEqual("NoSchema", doc.RootElement.GetProperty("Name").GetString());
		Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("Schema").ValueKind);
	}

	[TestMethod]
	public void DeserializeShouldParseSchemaFromStringInContainer()
	{
		// Arrange - container where Schema is a JSON string containing schema JSON
		string json = "{\"Name\":\"TestSchema\",\"Schema\":\"{\\\"type\\\":\\\"object\\\"}\"}";

		// Act
		SchemaContainer? deserialized = JsonSerializer.Deserialize<SchemaContainer>(json, SerializerOptions);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual("TestSchema", deserialized.Name);
		Assert.IsNotNull(deserialized.Schema);
		Assert.AreEqual(JsonObjectType.Object, deserialized.Schema.Type);
	}

	[TestMethod]
	public void DeserializeShouldHandleNullSchemaInContainer()
	{
		// Arrange
		string json = "{\"Name\":\"NoSchema\",\"Schema\":null}";

		// Act
		SchemaContainer? deserialized = JsonSerializer.Deserialize<SchemaContainer>(json, SerializerOptions);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual("NoSchema", deserialized.Name);
		Assert.IsNull(deserialized.Schema);
	}

	[TestMethod]
	[DataRow("\"not a schema\"")]
	[DataRow("\"{\\\"type\\\": \"")]
	public void DeserializeShouldThrowJsonExceptionForInvalidSchemaText(string json)
	{
		JsonException exception = Assert.ThrowsExactly<JsonException>(() =>
			JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions));

		Assert.IsNotNull(exception.InnerException, "The parser's own error should be kept as the inner exception.");
	}

	[TestMethod]
	public void DeserializeShouldThrowJsonExceptionForInvalidSchemaTextInContainer()
	{
		string json = """{"Name":"Bad","Schema":"not a schema"}""";

		JsonException exception = Assert.ThrowsExactly<JsonException>(() =>
			JsonSerializer.Deserialize<SchemaContainer>(json, SerializerOptions));

		Assert.AreEqual("$.Schema", exception.Path);
	}

	[TestMethod]
	public void SerializeAndDeserializeShouldRoundTripSchema()
	{
		JsonSchema schema = new()
		{
			Type = JsonObjectType.Object,
		};
		schema.Properties["name"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.String,
		};

		string json = JsonSerializer.Serialize(schema, SerializerOptions);
		JsonSchema? result = JsonSerializer.Deserialize<JsonSchema>(json, SerializerOptions);

		Assert.IsNotNull(result);
		Assert.AreEqual(JsonObjectType.Object, result.Type);
		Assert.IsTrue(result.Properties.ContainsKey("name"));
		Assert.AreEqual(JsonObjectType.String, result.Properties["name"].Type);
	}

	[TestMethod]
	public void SerializeAndDeserializeShouldRoundTripSchemaInContainer()
	{
		JsonSchema schema = new()
		{
			Type = JsonObjectType.Object,
		};
		schema.Properties["age"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.Integer,
		};
		SchemaContainer container = new()
		{
			Name = "Settings",
			Schema = schema,
		};

		string json = JsonSerializer.Serialize(container, SerializerOptions);
		SchemaContainer? result = JsonSerializer.Deserialize<SchemaContainer>(json, SerializerOptions);

		Assert.IsNotNull(result);
		Assert.AreEqual("Settings", result.Name);
		Assert.IsNotNull(result.Schema);
		Assert.AreEqual(JsonObjectType.Object, result.Schema.Type);
		Assert.AreEqual(JsonObjectType.Integer, result.Schema.Properties["age"].Type);
	}

	[TestMethod]
	public void CreateConverterShouldReturnConverterForJsonSchemaType()
	{
		// Act
		JsonConverter converter = factory.CreateConverter(typeof(JsonSchema), SerializerOptions);

		// Assert
		Assert.IsNotNull(converter);
	}
}

public class SchemaContainer
{
	public string Name { get; set; } = string.Empty;
	public JsonSchema? Schema { get; set; }
}

public class PropertyContainer
{
	public JsonSchemaProperty? Property { get; set; }
}
