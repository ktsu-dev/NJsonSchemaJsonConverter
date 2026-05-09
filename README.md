# ktsu.NJsonSchemaJsonConverter

> A `JsonConverterFactory` for System.Text.Json that enables seamless serialization and deserialization of NJsonSchema `JsonSchema` objects.

[![License](https://img.shields.io/github/license/ktsu-dev/NJsonSchemaJsonConverter)](https://github.com/ktsu-dev/NJsonSchemaJsonConverter/blob/main/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/v/ktsu.NJsonSchemaJsonConverter.svg)](https://www.nuget.org/packages/ktsu.NJsonSchemaJsonConverter/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.NJsonSchemaJsonConverter.svg)](https://www.nuget.org/packages/ktsu.NJsonSchemaJsonConverter/)
[![Build Status](https://github.com/ktsu-dev/NJsonSchemaJsonConverter/workflows/build/badge.svg)](https://github.com/ktsu-dev/NJsonSchemaJsonConverter/actions)
[![GitHub Stars](https://img.shields.io/github/stars/ktsu-dev/NJsonSchemaJsonConverter?style=social)](https://github.com/ktsu-dev/NJsonSchemaJsonConverter/stargazers)

## Introduction

`ktsu.NJsonSchemaJsonConverter` bridges the gap between [NJsonSchema](https://github.com/RicoSuter/NJsonSchema) and System.Text.Json. NJsonSchema provides its own JSON parsing and generation methods, but doesn't integrate natively with `System.Text.Json.Serialization`. This library provides a `JsonConverterFactory` that allows `JsonSchema` objects (and subclasses) to be serialized and deserialized as part of larger object graphs using `System.Text.Json.JsonSerializer`.

## Features

- **Full JsonSchema round-tripping** - Serialize and deserialize `JsonSchema` objects to/from JSON
- **Subclass support** - Automatically handles any type derived from `JsonSchema`
- **Drop-in integration** - Register as a converter in `JsonSerializerOptions` and it just works
- **Lightweight** - Single source file, minimal dependencies

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.NJsonSchemaJsonConverter
```

### .NET CLI

```bash
dotnet add package ktsu.NJsonSchemaJsonConverter
```

### Package Reference

```xml
<PackageReference Include="ktsu.NJsonSchemaJsonConverter" Version="x.y.z" />
```

## Usage Examples

### Basic Setup

Register the converter factory in your `JsonSerializerOptions`:

```csharp
using ktsu.NJsonSchemaJsonConverter;
using System.Text.Json;

var options = new JsonSerializerOptions
{
    Converters = { new NJsonSchemaJsonConverterFactory() }
};
```

### Serializing an Object Containing a JsonSchema

```csharp
using NJsonSchema;
using System.Text.Json;

public class SchemaContainer
{
    public string Name { get; set; } = string.Empty;
    public JsonSchema? Schema { get; set; }
}

// Create a schema
var schema = await JsonSchema.FromTypeAsync<MyModel>();

var container = new SchemaContainer
{
    Name = "MyModel Schema",
    Schema = schema
};

// Serialize - the schema is written as an inline JSON object
string json = JsonSerializer.Serialize(container, options);
```

### Deserializing an Object Containing a JsonSchema

```csharp
// Deserialize - the schema is parsed back into a JsonSchema object
var result = JsonSerializer.Deserialize<SchemaContainer>(json, options);

// result.Schema is a fully functional JsonSchema instance
Console.WriteLine(result?.Schema?.Properties.Count);
```

### Using with Dependency Injection

```csharp
services.AddSingleton(new JsonSerializerOptions
{
    Converters = { new NJsonSchemaJsonConverterFactory() }
});
```

## How It Works

The converter handles `JsonSchema` serialization in two directions:

- **Writing**: Calls `JsonSchema.ToJson()` to produce the schema's JSON representation and writes it inline using `WriteRawValue`
- **Reading**: Reads the JSON token as a string and parses it using `JsonSchema.FromJsonAsync()` to reconstruct the schema object

The factory uses reflection to create type-specific generic converter instances, supporting `JsonSchema` and any subclass.

## API Reference

### `NJsonSchemaJsonConverterFactory`

A `JsonConverterFactory` that creates converters for `JsonSchema` and its subclasses.

| Method                                          | Description                                                        |
|-------------------------------------------------|--------------------------------------------------------------------|
| `CanConvert(Type)`                              | Returns `true` for `JsonSchema` and any type deriving from it      |
| `CreateConverter(Type, JsonSerializerOptions)`  | Creates a type-specific `JsonConverter<T>` instance via reflection |

## Contributing

Contributions are welcome! Here's how you can help:

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please make sure to update tests as appropriate.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
