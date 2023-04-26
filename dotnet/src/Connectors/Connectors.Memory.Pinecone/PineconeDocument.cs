using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Http.ApiSchema;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Model;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone;

public class PineconeDocument
{
    private static int s_idCounter;
    private static string DefaultId => $"item-{s_idCounter++}";

    /// <summary>
    /// The unique ID of a Document
    /// </summary>
    /// <value>The unique ID of a document</value>
    /// <example>&quot;vector-0&quot;</example>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Vector dense data. This should be the same length as the dimension of the index being queried.
    /// </summary>
    /// <value>Vector dense data. This should be the same length as the dimension of the index being queried.</value>
    [JsonPropertyName("values")]
    public IEnumerable<float> Values { get; set; }

    /// <summary>
    /// The metadata associated with the document
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or Sets SparseValues
    /// </summary>
    [JsonPropertyName("sparseValues")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SparseVectorData? SparseValues { get; set; }

    /// <summary>
    /// Gets or Sets Score
    /// </summary>
    [JsonPropertyName("score")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float? Score { get; set; }

    /// <summary>
    ///  The text of the document, if the document was created from text.
    /// </summary>
    [JsonIgnore]
    public string? Text => this.Metadata?.TryGetValue("text", out var text) == true ? text.ToString() : null;

    /// <summary>
    /// The document ID, used to identify the source text this document was created from
    /// </summary>
    /// <remarks>
    ///  An important distinction between the document ID and ID / Key is that the document ID is
    ///  used to identify the source text this document was created from, while the ID / Key is used
    ///  to identify the document itself.
    /// </remarks>
    [JsonIgnore]
    public string? DocumentId => this.Metadata?.TryGetValue("document_Id", out var docId) == true ? docId.ToString() : null;

    /// <summary>
    ///  The source ID, used to identify the source text this document was created from.
    /// </summary>
    /// <remarks>
    ///  An important distinction between the source ID and the source of the document is that the source
    ///  may be Medium, Twitter, etc. while the source ID would be the ID of the Medium post, Twitter tweet, etc.
    /// </remarks>
    [JsonIgnore]
    public string? SourceId => this.Metadata?.TryGetValue("source_Id", out var sourceId) == true ? sourceId.ToString() : null;

    [JsonIgnore]
    public string? CreatedAt => this.Metadata?.TryGetValue("created_at", out var createdAt) == true ? createdAt.ToString() : null;

    public static PineconeDocument Create(string? id = default, IEnumerable<float>? values = default)
    {
        return new PineconeDocument(values, id);
    }

    internal UpdateVectorRequest ToUpdateRequest()
    {
        return UpdateVectorRequest.FromPineconeDocument(this);
    }

    public PineconeDocument WithSparseValues(SparseVectorData? sparseValues)
    {
        this.SparseValues = sparseValues;
        return this;
    }

    public PineconeDocument WithMetadata(Dictionary<string, object>? metadata)
    {
        this.Metadata = metadata;
        return this;
    }

    /// <summary>
    /// Serializes the metadata to JSON.
    /// </summary>
    /// <returns></returns>
    public string GetSerializedMetadata()
    {
        // return a dictionary from the metadata without the text, document_Id, and source_Id properties

        if (this.Metadata == null)
        {
            return string.Empty;
        }

        Dictionary<string, object> distinctMetaData = this.Metadata
            .Where(x => x.Key != "text" && x.Key != "document_Id" && x.Key != "source_Id" && x.Key != "created_at")
            .ToDictionary(x => x.Key, x => x.Value);
        return JsonSerializer.Serialize(distinctMetaData);
    }

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    /// <inheritdoc />
    public override string ToString()
    {
        return string.Join(Environment.NewLine, this.GetType().GetProperties().Select(p => $"{p.Name}: {p.GetValue(this)}"));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PineconeDocument" /> class.
    /// </summary>
    /// <param name="id">The unique ID of a vector.</param>
    /// <param name="values">Vector dense data. This should be the same length as the dimension of the index being queried..</param>
    /// <param name="sparseValues">sparseValues.</param>
    /// <param name="metadata">metadata.</param>
    /// <param name="score"></param>
    [JsonConstructor]
    public PineconeDocument(
        IEnumerable<float>? values = null,
        string? id = default,
        Dictionary<string, object>? metadata = null,
        SparseVectorData? sparseValues = null,
        float? score = null)
    {
        this.Id = id ?? DefaultId;
        this.Values = values ?? Array.Empty<float>();
        this.Metadata = metadata ?? new Dictionary<string, object>();
        this.SparseValues = sparseValues;
        this.Score = score;
    }

}
