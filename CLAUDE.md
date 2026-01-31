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

## Notes

- No test project currently exists
- The converter expects schema JSON as a string value when reading (not as an embedded JSON object)
- Uses synchronous `.Result` on async `FromJsonAsync()` - standard pattern for JsonConverter which doesn't support async
