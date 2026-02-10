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
