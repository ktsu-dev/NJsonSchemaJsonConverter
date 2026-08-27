# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ktsu.NJsonSchemaJsonConverter** is a .NET library that provides a `JsonConverterFactory` for System.Text.Json to handle serialization and deserialization of NJsonSchema `JsonSchema` objects. It enables seamless JSON round-tripping of schema definitions within objects serialized via System.Text.Json.

## Build Commands

```bash
# Build the project
dotnet build

# Build for release
dotnet build --configuration Release

# Create NuGet package
dotnet pack --configuration Release --output ./staging
```

## Architecture

The library consists of a single source file with two classes:

- **NJsonSchemaJsonConverterFactory** - A `JsonConverterFactory` implementation that:
  - Handles `JsonSchema` and any subclass of `JsonSchema`
  - Uses reflection to create generic converter instances

- **NJsonSchemaJsonConverter\<T\>** (nested private class) - A `JsonConverter<T>` that:
  - **Read**: Expects JSON strings containing schema JSON, parses via `JsonSchema.FromJsonAsync()`
  - **Write**: Outputs the schema's JSON representation via `JsonSchema.ToJson()`

## Dependencies

- **NJsonSchema** - The schema library being converted
- **ktsu.Extensions** - ktsu extension methods
- **Polyfill** - Backports modern .NET APIs to older frameworks

## SDK and Build System

This project uses **ktsu.Sdk**, a custom MSBuild SDK that provides standardized build configuration. Package versions are managed centrally via `Directory.Packages.props`.

Multi-targeting: net10.0, net9.0, net8.0, net7.0, net6.0, net5.0, netstandard2.0, netstandard2.1

## Testing

`NJsonSchemaJsonConverter.Test` uses **MSTest.Sdk** with the Microsoft Testing Platform, and targets
`net10.0` only (it blanks the SDK's inherited `TargetFrameworks`), while the library itself
multi-targets all eight frameworks.

```bash
dotnet test
dotnet test --filter "FullyQualifiedName~DeserializeShouldThrowJsonExceptionForNonStringToken"
```

`NJsonSchemaJsonConverterFactoryTests` covers four areas:

- **`CanConvert`** — accepts `JsonSchema`, rejects `string`, `int` and `object`.
- **`CreateConverter`** — returns a converter for `JsonSchema`.
- **Round-tripping a bare schema** — serialize and deserialize, both simple and with properties.
- **Round-tripping a schema nested in a container object** — including the null-schema case in
  both directions, which is the shape real consumers hit.

Note that the suite exercises only `net10.0`. The `netstandard2.0`/`netstandard2.1` assets are
built but never run.

## Notes

- The converter expects schema JSON as a **string value** when reading, not an embedded JSON
  object. `Read` checks `reader.TokenType == JsonTokenType.String` and throws `JsonException`
  otherwise; `DeserializeShouldThrowJsonExceptionForNonStringToken` pins that.
- `Write` uses `writer.WriteRawValue(...)`, so the schema is emitted **inline** as JSON rather
  than as an escaped string — the two directions are deliberately asymmetric. Reading back
  output produced by this converter therefore requires the schema to have been re-encoded as a
  string first.
- Uses synchronous `.Result` on async `FromJsonAsync()` — the standard pattern for a
  `JsonConverter`, which has no async read path.
