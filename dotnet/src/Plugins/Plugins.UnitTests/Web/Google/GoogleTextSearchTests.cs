﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Http;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using Microsoft.SemanticKernel.Search;
using Xunit;

namespace SemanticKernel.Plugins.UnitTests.Web.Google;

public sealed class GoogleTextSearchTests : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleTextSearchTests"/> class.
    /// </summary>
    public GoogleTextSearchTests()
    {
        this._messageHandlerStub = new MultipleHttpMessageHandlerStub();
        this._clientFactory = new CustomHttpClientFactory(this._messageHandlerStub);
        this._kernel = new Kernel();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._messageHandlerStub.Dispose();

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SearchReturnsSuccessfullyAsync()
    {
        // Arrange
        this._messageHandlerStub.AddJsonResponse(File.ReadAllText(WhatIsTheSKResponseJson));

        // Create an ITextSearch instance using Google search
        using var textSearch = new GoogleTextSearch(
            initializer: new() { ApiKey = "ApiKey", HttpClientFactory = this._clientFactory },
            searchEngineId: "SearchEngineId");

        // Act
        KernelSearchResults<string> result = await textSearch.SearchAsync("What is the Semantic Kernel?", new() { Count = 4, Offset = 0 });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Results);
        var resultList = await result.Results.ToListAsync();
        Assert.NotNull(resultList);
        Assert.Equal(4, resultList.Count);
        foreach (var stringResult in resultList)
        {
            Assert.NotEmpty(stringResult);
        }
    }

    [Fact]
    public async Task GetTextSearchResultsReturnsSuccessfullyAsync()
    {
        // Arrange
        this._messageHandlerStub.AddJsonResponse(File.ReadAllText(WhatIsTheSKResponseJson));

        // Create an ITextSearch instance using Google search
        using var textSearch = new GoogleTextSearch(
            initializer: new() { ApiKey = "ApiKey", HttpClientFactory = this._clientFactory },
            searchEngineId: "SearchEngineId");

        // Act
        KernelSearchResults<TextSearchResult> result = await textSearch.GetTextSearchResultsAsync("What is the Semantic Kernel?", new() { Count = 10, Offset = 0 });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Results);
        var resultList = await result.Results.ToListAsync();
        Assert.NotNull(resultList);
        Assert.Equal(4, resultList.Count);
        foreach (var textSearchResult in resultList)
        {
            Assert.NotNull(textSearchResult.Name);
            Assert.NotNull(textSearchResult.Value);
            Assert.NotNull(textSearchResult.Link);
        }
    }

    [Fact]
    public async Task GetSearchResultsReturnsSuccessfullyAsync()
    {
        // Arrange
        this._messageHandlerStub.AddJsonResponse(File.ReadAllText(WhatIsTheSKResponseJson));

        // Create an ITextSearch instance using Google search
        using var textSearch = new GoogleTextSearch(
            initializer: new() { ApiKey = "ApiKey", HttpClientFactory = this._clientFactory },
            searchEngineId: "SearchEngineId");

        // Act
        KernelSearchResults<object> results = await textSearch.GetSearchResultsAsync("What is the Semantic Kernel?", new() { Count = 10, Offset = 0 });

        // Assert
        Assert.NotNull(results);
        Assert.NotNull(results.Results);
        var resultList = await results.Results.ToListAsync();
        Assert.NotNull(resultList);
        Assert.Equal(4, resultList.Count);
        foreach (Result result in resultList)
        {
            Assert.NotNull(result.Title);
            Assert.NotNull(result.Snippet);
            Assert.NotNull(result.Link);
            Assert.NotNull(result.DisplayLink);
            Assert.NotNull(result.Kind);
        }
    }

    [Theory]
    [InlineData("cr", "countryAF", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cr=countryAF&cx=SearchEngineId&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("dateRestrict", "d[5]", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&dateRestrict=d%5B5%5D&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("exactTerms", "Semantic Kernel", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&exactTerms=Semantic%20Kernel&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("excludeTerms", "FooBar", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&excludeTerms=FooBar&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("filter", "0", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&filter=0&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("gl", "ie", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&gl=ie&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("hl", "en", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&hl=en&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("linkSite", "http://example.com", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&linkSite=http%3A%2F%2Fexample.com&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("lr", "lang_ar", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&lr=lang_ar&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("orTerms", "Microsoft", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&num=4&orTerms=Microsoft&q=What%20is%20the%20Semantic%20Kernel%3F&start=0")]
    [InlineData("rights", "cc_publicdomain", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&rights=cc_publicdomain&start=0")]
    [InlineData("siteSearch", "devblogs.microsoft.com", "https://customsearch.googleapis.com/customsearch/v1?key=ApiKey&cx=SearchEngineId&num=4&q=What%20is%20the%20Semantic%20Kernel%3F&siteSearch=devblogs.microsoft.com&siteSearchFilter=i&start=0")]
    public async Task BuildsCorrectUriForEqualityFilterAsync(string paramName, object paramValue, string requestLink)
    {
        // Arrange
        this._messageHandlerStub.AddJsonResponse(File.ReadAllText(SiteFilterDevBlogsResponseJson));

        // Create an ITextSearch instance using Google search
        using var textSearch = new GoogleTextSearch(
            initializer: new() { ApiKey = "ApiKey", HttpClientFactory = this._clientFactory },
            searchEngineId: "SearchEngineId");

        // Act
        TextSearchOptions searchOptions = new() { Count = 4, Offset = 0, BasicFilter = new BasicFilterOptions().Equality(paramName, paramValue) };
        KernelSearchResults<object> result = await textSearch.GetSearchResultsAsync("What is the Semantic Kernel?", searchOptions);

        // Assert
        var requestUris = this._messageHandlerStub.RequestUris;
        Assert.Single(requestUris);
        Assert.NotNull(requestUris[0]);
        var absoluteUri = requestUris[0]!.AbsoluteUri;
        Assert.Equal(requestLink, requestUris[0]!.AbsoluteUri);
    }

    [Fact]
    public async Task DoesNotBuildsUriForInvalidQueryParameterAsync()
    {
        // Arrange
        this._messageHandlerStub.AddJsonResponse(File.ReadAllText(SiteFilterDevBlogsResponseJson));
        TextSearchOptions searchOptions = new() { Count = 4, Offset = 0, BasicFilter = new BasicFilterOptions().Equality("fooBar", "Baz") };

        using var textSearch = new GoogleTextSearch(
            initializer: new() { ApiKey = "ApiKey", HttpClientFactory = this._clientFactory },
            searchEngineId: "SearchEngineId");

        // Act && Assert
        var e = await Assert.ThrowsAsync<ArgumentException>(async () => await textSearch.GetSearchResultsAsync("What is the Semantic Kernel?", searchOptions));
        Assert.Equal("Unknown equality filter clause field name, must be one of cr,dateRestrict,exactTerms,excludeTerms,filter,gl,hl,linkSite,lr,orTerms,rights,siteSearch (Parameter 'searchOptions')", e.Message);
    }

    #region private
    private const string WhatIsTheSKResponseJson = "./TestData/google_what_is_the_semantic_kernel.json";
    private const string SiteFilterDevBlogsResponseJson = "./TestData/google_site_filter_devblogs_microsoft.com.json";

    private readonly MultipleHttpMessageHandlerStub _messageHandlerStub;
    private readonly CustomHttpClientFactory _clientFactory;
    private readonly Kernel _kernel;

    /// <summary>
    /// Implementation of <see cref="Google.Apis.Http.IHttpClientFactory"/> which uses the <see cref="LoggingConfigurableMessageHandler"/>.
    /// </summary>
    private class CustomHttpClientFactory(MultipleHttpMessageHandlerStub handlerStub) : IHttpClientFactory
    {
        private readonly MultipleHttpMessageHandlerStub _handlerStub = handlerStub;

        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)
        {
            ConfigurableMessageHandler messageHandler = new(this._handlerStub);
            var configurableHttpClient = new ConfigurableHttpClient(messageHandler);
            return configurableHttpClient;
        }
    }
    #endregion
}
