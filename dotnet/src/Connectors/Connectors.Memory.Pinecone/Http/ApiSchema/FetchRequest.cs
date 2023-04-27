using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Http.ApiSchema;

/// <summary>
/// FetchRequest
/// See https://docs.pinecone.io/reference/fetch
/// </summary>
internal class FetchRequest
{

    /// <summary>
    /// Gets or Sets Ids
    /// </summary>
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; }

    /// <summary>
    /// An index namespace name
    /// </summary>
    [JsonPropertyName("namespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Namespace { get; set; }

    public static FetchRequest FetchVectors(IEnumerable<string> ids)
    {
        return new FetchRequest(ids);
    }

    public FetchRequest FromNamespace(string nameSpace)
    {
        this.Namespace = nameSpace;
        return this;
    }

    public HttpRequestMessage Build()
    {
        string? path = "/vectors/fetch?ids=" + this.Ids[0];

        for (int i = 1; i < this.Ids.Count; i++)
        {
            path += "&ids=" + this.Ids[i];
        }

        if (!string.IsNullOrEmpty(this.Namespace))
        {
            path += $"&namespace={this.Namespace}";
        }

        return HttpRequest.CreateGetRequest(path);
    }

    #region private ================================================================================

    /// <summary>
    /// Initializes a new instance of the <see cref="FetchRequest" /> class.
    /// </summary>
    private FetchRequest(IEnumerable<string> ids)
    {
        this.Ids = ids.ToList();
    }

    #endregion

}
