// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.NJsonSchemaJsonConverter;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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
				//
				// A $ref to a definition whose name holds a '/' only resolves with the '/' escaped as
				// "~1". Earlier versions of Write left it unescaped, so escape such pointers here,
				// letting the data they saved load again.
				T? schema = JsonSchemaSerialization.FromJsonAsync<T>(
					EscapeLocalReferences(json, options.MaxDepth),
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
			//
			// ToJson writes a $ref to a definition named "Foo/Bar" as "#/definitions/Foo/Bar", which
			// reads as a path through "Foo" to "Bar". Escape it as JSON Pointer requires, so the
			// schema can be read back.
			JsonDocumentOptions documentOptions = new() { MaxDepth = options.MaxDepth };
			using JsonDocument document = JsonDocument.Parse(EscapeLocalReferences(ToJson((JsonSchema)(object)value), options.MaxDepth), documentOptions);
			document.WriteTo(writer);
		}

		// Rewrites each local "$ref" in a schema document that doesn't resolve as a JSON Pointer
		// (RFC 6901) but does once some of its '/' separators are read as part of a key, as they are
		// in "#/definitions/Foo/Bar" for a definition named "Foo/Bar". The rewritten pointer escapes
		// '%', '~' and '/' in each key ("#/definitions/Foo~1Bar"), which NJsonSchema decodes back to
		// the key after splitting the pointer. Pointers that already resolve, or that can't be
		// resolved either way, are left as they are, and the text is returned unchanged when no
		// pointer needed rewriting.
		private static string EscapeLocalReferences(string json, int maxDepth)
		{
			if (!json.Contains("\"$ref\"", StringComparison.Ordinal))
			{
				return json;
			}

			JsonDocumentOptions documentOptions = new() { MaxDepth = maxDepth };
			JsonNode? root = JsonNode.Parse(json, nodeOptions: null, documentOptions);
			if (root is null)
			{
				return json;
			}

			bool changed = false;
			EscapeLocalReferences(root, root, ref changed);
			return changed ? root.ToJsonString() : json;
		}

		private static void EscapeLocalReferences(JsonNode root, JsonNode? node, ref bool changed)
		{
			if (node is JsonObject jsonObject)
			{
				foreach (KeyValuePair<string, JsonNode?> property in jsonObject.ToList())
				{
					if (property.Key == "$ref"
						&& property.Value is JsonValue value
						&& value.TryGetValue(out string? pointer)
						&& EscapeLocalPointer(root, pointer) is string escaped)
					{
						jsonObject["$ref"] = escaped;
						changed = true;
					}
					else
					{
						EscapeLocalReferences(root, property.Value, ref changed);
					}
				}
			}
			else if (node is JsonArray jsonArray)
			{
				foreach (JsonNode? item in jsonArray)
				{
					EscapeLocalReferences(root, item, ref changed);
				}
			}
		}

		// Returns the escaped form of a local pointer that only resolves with some separators read as
		// part of a key, or null when it needs no rewriting or can't be resolved.
		private static string? EscapeLocalPointer(JsonNode root, string pointer)
		{
			if (!pointer.StartsWith("#/", StringComparison.Ordinal))
			{
				return null;
			}

			string[] segments = pointer[2..].Split('/');
			JsonNode? node = root;
			foreach (string segment in segments)
			{
				node = Child(node, UnescapeSegment(segment));
				if (node is null)
				{
					break;
				}
			}

			if (node is not null)
			{
				return null;
			}

			List<string> keys = [];
			return MatchKeys(root, segments, 0, keys, new HashSet<(JsonNode, int)>(ReferenceTupleComparer.Instance))
				? "#/" + string.Join("/", keys.Select(EscapeSegment))
				: null;
		}

		// Finds keys that consume segments[start..] from node, each key one or more consecutive
		// segments joined by '/', trying the fewest segments per key first. failed remembers where
		// the rest of the pointer can't be matched, so the search stays polynomial.
		private static bool MatchKeys(JsonNode node, string[] segments, int start, List<string> keys, HashSet<(JsonNode, int)> failed)
		{
			if (start == segments.Length)
			{
				return true;
			}

			if (failed.Contains((node, start)))
			{
				return false;
			}

			for (int end = start + 1; end <= segments.Length; end++)
			{
				string raw = string.Join("/", segments, start, end - start);
				string unescaped = UnescapeSegment(raw);
				foreach (string key in unescaped == raw ? [raw] : new[] { raw, unescaped })
				{
					if (Child(node, key) is JsonNode child)
					{
						keys.Add(key);
						if (MatchKeys(child, segments, end, keys, failed))
						{
							return true;
						}

						keys.RemoveAt(keys.Count - 1);
					}
				}
			}

			failed.Add((node, start));
			return false;
		}

		private static JsonNode? Child(JsonNode? node, string key) => node switch
		{
			JsonObject jsonObject => jsonObject.TryGetPropertyValue(key, out JsonNode? child) ? child : null,
			JsonArray jsonArray => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index < jsonArray.Count ? jsonArray[index] : null,
			_ => null,
		};

		// Decodes a pointer segment as NJsonSchema's resolver does.
		private static string UnescapeSegment(string segment) =>
			Uri.UnescapeDataString(segment).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

		private static string EscapeSegment(string key) =>
			key.Replace("%", "%25", StringComparison.Ordinal).Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

		private sealed class ReferenceTupleComparer : IEqualityComparer<(JsonNode Node, int Start)>
		{
			public static ReferenceTupleComparer Instance { get; } = new();

			public bool Equals((JsonNode Node, int Start) x, (JsonNode Node, int Start) y) =>
				ReferenceEquals(x.Node, y.Node) && x.Start == y.Start;

			public int GetHashCode((JsonNode Node, int Start) obj) =>
				HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Node), obj.Start);
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
