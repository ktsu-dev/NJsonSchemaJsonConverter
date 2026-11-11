// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.NJsonSchemaJsonConverter;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Infrastructure;
using NJsonSchema.References;
using NJsonSchema.Visitors;

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
		where T : JsonSchema
	{
		private static readonly PropertyRenameAndIgnoreSerializerContractResolver ContractResolver = JsonSchema.CreateJsonSerializerContractResolver(SchemaType.JsonSchema);

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

			// Boolean schemas are valid JSON Schema (draft 6+), but NJsonSchema only parses an object,
			// so they are built here: true accepts anything, false rejects everything.
			if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
			{
				T schema;
				try
				{
					schema = Activator.CreateInstance<T>();
				}
				catch (MissingMethodException ex)
				{
					// A subclass with no public parameterless constructor can't be built here. Report
					// it as a JsonException, as the object path does for the same type, so callers
					// that handle JsonException see it and System.Text.Json can add the path.
					throw new JsonException("Invalid JSON schema.", ex);
				}

				if (reader.TokenType is JsonTokenType.False)
				{
					schema.Not = new JsonSchema();
				}

				return schema;
			}

			// Write emits the schema as an inline object, so accept that. A string token holding
			// escaped schema text is still accepted, so data written in that form keeps loading.
			string json = reader.TokenType switch
			{
				JsonTokenType.StartObject => ReadRawValue(ref reader),
				JsonTokenType.String => reader.GetString()!,
				_ => throw new JsonException(),
			};

			try
			{
				// Deserialize into T itself rather than JsonSchema, so a subclass such as
				// JsonSchemaProperty comes back as that type instead of failing the cast. This is what
				// JsonSchema.FromJsonAsync does, generalized over the target type.
				T? schema = JsonSchemaSerialization.FromJsonAsync<T>(
					json,
					SchemaType.JsonSchema,
					documentPath: null,
					schema => new LocalOnlyJsonReferenceResolver(new JsonSchemaAppender(schema, new DefaultTypeNameGenerator())),
					ContractResolver,
					CancellationToken.None).GetAwaiter().GetResult();

				// Empty, whitespace-only or "null" text parses to no schema at all rather than
				// failing. A real JSON null never reaches Read, so this is malformed input; report it
				// here instead of leaving a null in a non-nullable property.
				//
				// NJsonSchema also doesn't check for reference cycles while parsing, so a $ref chain that
				// never reaches a real schema loads fine and then throws on the first Validate or
				// Write. Dereference every schema now, so the catch below reports it as malformed.
				new ReferenceCycleCheck().Visit(schema ?? throw new JsonException("Invalid JSON schema."));
				return schema;
			}
			catch (Exception ex) when (ex is not JsonException)
			{
				// Report a schema that fails to parse as a JsonException, like any other malformed
				// input, so callers that handle JsonException see it and System.Text.Json can add
				// the path and position.
				throw new JsonException("Invalid JSON schema.", ex);
			}
		}

		// NJsonSchema's default resolver fetches an http(s) $ref over the network, blocking the
		// deserializing thread and inlining whatever comes back. Deserializing must stay a local
		// operation on caller-supplied input, so URL references are refused; the catch in Read
		// reports that as a JsonException. File references need no override: Read passes no document
		// path, so NJsonSchema rejects them before it would load anything.
		private sealed class LocalOnlyJsonReferenceResolver(JsonSchemaAppender schemaAppender) : JsonReferenceResolver(schemaAppender)
		{
			public override Task<IJsonReference> ResolveUrlReferenceAsync(string url, CancellationToken cancellationToken = default) =>
				throw new NotSupportedException($"Resolving the external schema reference '{url}' is not supported.");
		}

		// Dereferences every schema in the graph. ActualSchema throws InvalidOperationException for
		// a reference cycle, and returns normally for valid recursion such as "$ref": "#".
		private sealed class ReferenceCycleCheck : JsonSchemaVisitorBase
		{
			protected override JsonSchema VisitSchema(JsonSchema schema, string path, string? typeNameHint)
			{
				_ = schema.ActualSchema;
				return schema;
			}
		}

		private static string ReadRawValue(ref Utf8JsonReader reader)
		{
			using JsonDocument document = JsonDocument.ParseValue(ref reader);
			return document.RootElement.GetRawText();
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

			// NJsonSchema's ToJson always indents, so writing its text raw would split a compact
			// document (JSON Lines, log lines) across lines, and in an indented one would restart
			// the schema at column 0. Re-emit it through the writer so it follows the writer's
			// own formatting. Parse at the caller's MaxDepth (0 means the default of 64): the
			// default document limit would otherwise refuse a deep schema the caller allowed, and
			// the writer still enforces the caller's limit on the document as a whole.
			JsonDocumentOptions documentOptions = new() { MaxDepth = options.MaxDepth };
			using JsonDocument document = JsonDocument.Parse(ToJson((JsonSchema)(object)value), documentOptions);
			document.WriteTo(writer);
		}

		// NJsonSchema can only write a $ref as a path into the schema being written. A sub-schema
		// taken from a parent (JsonSchema.FromType<T>().Properties["X"]) can refer to the parent's
		// Definitions, and ToJson then throws InvalidOperationException. Report that as a
		// JsonException, like malformed input on the read side, so callers that handle JsonException
		// see it and System.Text.Json can add the path of the member being written.
		private static string ToJson(JsonSchema schema)
		{
			try
			{
				return schema.ToJson();
			}
			catch (InvalidOperationException ex)
			{
				throw new JsonException(
					"The JSON schema could not be written. A schema that references definitions held by a parent schema must be written as part of that parent schema.",
					ex);
			}
		}
	}
}
