﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Xunit;

namespace SemanticKernel.Functions.UnitTests.OpenAPI;

public class RestApiOperationResponseTests
{
    [Fact]
    public void ItShouldValidateStringContentWithNoSchema()
    {
        //Arrange
        var response = new RestApiOperationResponse("fake-content", "fake-content-type");

        //Act
        var result = response.IsValid();

        //Assert
        Assert.True(result);
    }

    [Fact]
    public void ItShouldValidateByteContentTWithNoSchema()
    {
        //Arrange
        var response = new RestApiOperationResponse(new byte[] { 00, 01, 02 }, "fake-content-type");

        //Act
        var result = response.IsValid();

        //Assert
        Assert.True(result);
    }

    [Fact]
    public void ItShouldFailValidationWithSchema()
    {
        //Arrange
        var response = new RestApiOperationResponse("fake-content", "fake-content-type", JsonDocument.Parse("{\"type\": \"string\"}"));

        //Act
        var result = response.IsValid();

        //Assert
        Assert.False(result); // todo -- shouldn't this validate actually?
    }

    [Fact]
    public void ItShouldPassValidationWithSchema()
    {
        //Arrange
        var response = new RestApiOperationResponse(
            "\"fake-content\"",
            "fake-content-type",
            JsonDocument.Parse("{\"type\": \"string\"}"));

        //Act
        var result = response.IsValid();

        //Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("{\"products\": [{\"id\": \"1234\", \"name\": \"Laptop\"}]}", "application/json", "{\"type\": \"object\"}")] // TODO -- Is this good enough/complete?
    [InlineData("{\"products\": [{\"id\": \"1234\", \"name\": \"Laptop\"}]}", "application/json", "{\"title\":\"ProductResponse\",\"type\":\"object\",\"properties\":{\"products\":{\"type\":\"array\",\"items\":{\"title\":\"Product\",\"type\":\"object\",\"properties\":{\"attributes\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"name\":{\"type\":\"string\"},\"price\":{\"type\":\"string\"},\"url\":{\"type\":\"string\"}}}}}}")]
    public void IsValidShouldBeTrue(object content, string contentType, string schemaJson)
    {
        //Arrange
        var response = new RestApiOperationResponse(content, contentType, JsonDocument.Parse(schemaJson));

        //Act
        var result = response.IsValid();

        //Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("{\"p\": [{\"id\": \"1234\", \"name\": \"Laptop\"}", "application/json", "{\"title\":\"ProductResponse\",\"type\":\"object\",\"properties\":{\"products\":{\"type\":\"array\",\"items\":{\"title\":\"Product\",\"type\":\"object\",\"properties\":{\"attributes\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"name\":{\"type\":\"string\"},\"price\":{\"type\":\"string\"},\"url\":{\"type\":\"string\"}}}}}}")]
    [InlineData("{\"products\": [{\"id\": \"1234\", \"name\": \"Laptop\"}", "application/json", "{\"title\":\"ProductResponse\",\"type\":\"object\",\"properties\":{\"products\":{\"type\":\"array\",\"items\":{\"title\":\"Product\",\"type\":\"object\",\"properties\":{\"attributes\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"name\":{\"type\":\"string\"},\"price\":{\"type\":\"string\"},\"url\":{\"type\":\"string\"}}}}}}")]
    public void IsValidShouldBeFalse(object content, string contentType, string schemaJson)
    {
        //Arrange
        var response = new RestApiOperationResponse(content, contentType, JsonDocument.Parse(schemaJson));

        //Act
        var result = response.IsValid();

        //Assert
        Assert.False(result);
    }
}
