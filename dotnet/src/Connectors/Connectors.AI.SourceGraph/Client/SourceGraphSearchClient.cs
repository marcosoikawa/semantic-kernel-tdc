// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.SemanticKernel.Connectors.AI.SourceGraph.Client;

using Extensions;
using global::Connectors.AI.SourceGraph;
using StrawberryShake;


public class SourceGraphSearchClient : ISourceGraphSearchClient
{

    private readonly ISourceGraphClient _sourceGraphClient;

    public SourceGraphSearchClient(ISourceGraphClient sourceGraphClient) => _sourceGraphClient = sourceGraphClient;


    /// <inheritdoc />
    public async Task<SearchResult?> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        IOperationResult<ICodeSearchQueryResult> queryResponse = await _sourceGraphClient.CodeSearchQuery.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
        queryResponse.EnsureNoErrors();

        return queryResponse.ToSearchResult();
    }


    /// <inheritdoc />
    public async Task<EmbeddingsSearch?> SearchEmbeddingsAsync(string repoId, string query, int codeResultsCount, int textResultsCount, CancellationToken cancellationToken = default)
    {
        IOperationResult queryResponse = await _sourceGraphClient.EmbeddingsSearchQuery.ExecuteAsync(repoId, query, codeResultsCount, textResultsCount, cancellationToken).ConfigureAwait(false);
        var response = queryResponse.ToSourceGraphResponse();
        return response.Data.EmbeddingsSearch;
    }


    /// <inheritdoc />
    public async Task<EmbeddingsSearch?> EmbeddingsSearchAsync(IEnumerable<string> repoIds, string query, int codeResultsCount, int textResultsCount, CancellationToken cancellationToken = default)
    {
        IOperationResult<IMultiEmbeddingsSearchQueryResult> queryResponse = await _sourceGraphClient.MultiEmbeddingsSearchQuery.ExecuteAsync(repoIds.ToList(), query, codeResultsCount, textResultsCount, cancellationToken).ConfigureAwait(false);
        queryResponse.EnsureNoErrors();
        var response = queryResponse.ToSourceGraphResponse();
        return response.Data.EmbeddingsMultiSearch;
    }

}
