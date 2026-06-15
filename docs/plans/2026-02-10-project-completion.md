# NJsonSchemaJsonConverter Project Completion Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete the NJsonSchemaJsonConverter project by adding a test project with comprehensive coverage and the missing TAGS.md metadata file.

**Architecture:** The test project follows the standard ktsu pattern: MSTest.Sdk + ktsu.Sdk, targeting net10.0 only, with a ProjectReference to the main library. Tests exercise the `NJsonSchemaJsonConverterFactory` (CanConvert, CreateConverter) and the round-trip serialize/deserialize behavior through `JsonSerializer`.

**Tech Stack:** MSTest, System.Text.Json, NJsonSchema, .NET 10.0

---

### Task 1: Create TAGS.md

**Files:**
- Create: `TAGS.md`

**Step 1: Create the tags file**

Create `TAGS.md` at the solution root with NuGet package tags:

```
json schema njsonschema serialization system-text-json converter jsonconverter dotnet csharp
```

**Step 2: Verify build still works**

Run: `dotnet build NJsonSchemaJsonConverter/NJsonSchemaJsonConverter.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add TAGS.md
git commit -m "Add TAGS.md for NuGet package discoverability"
```

---

### Task 2: Create the test project file

**Files:**
- Create: `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj`

**Step 1: Create test project directory and csproj**

Create `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj`:

```xml
<Project>
  <Sdk Name="MSTest.Sdk" />
  <Sdk Name="ktsu.Sdk" />

  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <TargetFramework>net10.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\NJsonSchemaJsonConverter\NJsonSchemaJsonConverter.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Add test project to solution**

Run: `dotnet sln NJsonSchemaJsonConverter.sln add NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj`
Expected: Project added to solution

**Step 3: Verify it builds**

Run: `dotnet build`
Expected: Build succeeded (both projects)

**Step 4: Commit**

```bash
git add NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj NJsonSchemaJsonConverter.sln
git commit -m "Add test project scaffold"
```

---

### Task 3: Write CanConvert tests

**Files:**
- Create: `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs`

**Step 1: Write the failing tests**

Create `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs`:

```csharp
// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ktsu.NJsonSchemaJsonConverter.Test;

using NJsonSchema;

[TestClass]
public class NJsonSchemaJsonConverterFactoryTests
{
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
}
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj --verbosity normal`
Expected: 4 tests passed

**Step 3: Commit**

```bash
git add NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs
git commit -m "Add CanConvert tests for NJsonSchemaJsonConverterFactory"
```

---

### Task 4: Write round-trip serialization tests

**Files:**
- Modify: `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs`

**Step 1: Write the failing tests**

Add these test methods to the existing `NJsonSchemaJsonConverterFactoryTests` class:

```csharp
	[TestMethod]
	public void SerializeAndDeserializeShouldRoundTripSimpleSchema()
	{
		// Arrange
		var schema = new JsonSchema
		{
			Type = JsonObjectType.String,
		};

		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act
		string json = System.Text.Json.JsonSerializer.Serialize(schema, options);
		var deserialized = System.Text.Json.JsonSerializer.Deserialize<JsonSchema>(json, options);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual(JsonObjectType.String, deserialized.Type);
	}

	[TestMethod]
	public void SerializeAndDeserializeShouldRoundTripSchemaWithProperties()
	{
		// Arrange
		var schema = new JsonSchema
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

		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act
		string json = System.Text.Json.JsonSerializer.Serialize(schema, options);
		var deserialized = System.Text.Json.JsonSerializer.Deserialize<JsonSchema>(json, options);

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
		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act & Assert
		Assert.ThrowsException<System.Text.Json.JsonException>(() =>
			System.Text.Json.JsonSerializer.Deserialize<JsonSchema>(json, options));
	}
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj --verbosity normal`
Expected: 7 tests passed

**Step 3: Commit**

```bash
git add NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs
git commit -m "Add round-trip serialization tests"
```

---

### Task 5: Write edge case and container tests

**Files:**
- Modify: `NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs`

**Step 1: Write the edge case tests**

Add these test methods to the existing class:

```csharp
	[TestMethod]
	public void SerializeShouldWriteValidJsonSchemaOutput()
	{
		// Arrange
		var schema = new JsonSchema
		{
			Type = JsonObjectType.String,
		};

		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act
		string json = System.Text.Json.JsonSerializer.Serialize(schema, options);

		// Assert - should be parseable as JSON
		Assert.IsNotNull(json);
		Assert.IsFalse(string.IsNullOrWhiteSpace(json));

		// Verify it's valid JSON by parsing it
		using var doc = System.Text.Json.JsonDocument.Parse(json);
		Assert.IsNotNull(doc);
	}

	[TestMethod]
	public void SerializeAndDeserializeShouldRoundTripSchemaInsideContainer()
	{
		// Arrange
		var schema = new JsonSchema
		{
			Type = JsonObjectType.Object,
		};
		schema.Properties["id"] = new JsonSchemaProperty
		{
			Type = JsonObjectType.Integer,
		};

		var container = new SchemaContainer
		{
			Name = "TestSchema",
			Schema = schema,
		};

		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act
		string json = System.Text.Json.JsonSerializer.Serialize(container, options);
		var deserialized = System.Text.Json.JsonSerializer.Deserialize<SchemaContainer>(json, options);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual("TestSchema", deserialized.Name);
		Assert.IsNotNull(deserialized.Schema);
		Assert.AreEqual(JsonObjectType.Object, deserialized.Schema.Type);
		Assert.IsTrue(deserialized.Schema.Properties.ContainsKey("id"));
	}

	[TestMethod]
	public void SerializeAndDeserializeShouldHandleNullSchemaInContainer()
	{
		// Arrange
		var container = new SchemaContainer
		{
			Name = "NoSchema",
			Schema = null,
		};

		var options = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new NJsonSchemaJsonConverterFactory() },
		};

		// Act
		string json = System.Text.Json.JsonSerializer.Serialize(container, options);
		var deserialized = System.Text.Json.JsonSerializer.Deserialize<SchemaContainer>(json, options);

		// Assert
		Assert.IsNotNull(deserialized);
		Assert.AreEqual("NoSchema", deserialized.Name);
		Assert.IsNull(deserialized.Schema);
	}
```

Also add the helper class at the bottom of the file, outside the test class:

```csharp
public class SchemaContainer
{
	public string Name { get; set; } = string.Empty;
	public JsonSchema? Schema { get; set; }
}
```

**Step 2: Run all tests to verify they pass**

Run: `dotnet test NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverter.Test.csproj --verbosity normal`
Expected: 10 tests passed

**Step 3: Commit**

```bash
git add NJsonSchemaJsonConverter.Test/NJsonSchemaJsonConverterFactoryTests.cs
git commit -m "Add edge case and container round-trip tests"
```

---

### Task 6: Run full build and test from solution root

**Files:**
- None (verification only)

**Step 1: Clean build the entire solution**

Run: `dotnet build --no-incremental`
Expected: Build succeeded for both projects

**Step 2: Run all tests**

Run: `dotnet test --verbosity normal`
Expected: All 10 tests passed

**Step 3: Final commit with all changes**

If any uncommitted changes remain:

```bash
git add -A
git commit -m "Complete project: add test project and TAGS.md"
```
