﻿// Copyright (c) Microsoft. All rights reserved.

/* 
Phase 02

- This class was created focused in the Image Generation using the SDK client instead of the own client in V1.
- Added Checking for empty or whitespace prompt.
- Removed the format parameter as this is never called in V1 code. Plan to implement it in the future once we change the ITextToImageService abstraction, using PromptExecutionSettings.
- Allow custom size for images when the endpoint is not the default OpenAI v1 endpoint.
*/

using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Images;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
internal partial class ClientCore
{
    /// <summary>
    /// Generates an image with the provided configuration.
    /// </summary>
    /// <param name="targetModel">Model identifier</param>
    /// <param name="prompt">Prompt to generate the image</param>
    /// <param name="width">Width of the image</param>
    /// <param name="height">Height of the image</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Url of the generated image</returns>
    internal async Task<string> GenerateImageAsync(
        string? targetModel,
        string prompt,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        Verify.NotNullOrWhiteSpace(prompt);

        var size = new GeneratedImageSize(width, height);

        var imageOptions = new ImageGenerationOptions()
        {
            Size = size,
            ResponseFormat = GeneratedImageFormat.Uri
        };

        // The model is not required by the OpenAI API and defaults to the DALL-E 2 server-side - https://platform.openai.com/docs/api-reference/images/create#images-create-model.
        // However, considering that the model is required by the OpenAI SDK and the ModelId property is optional, it defaults to DALL-E 2 in the line below.
        targetModel = string.IsNullOrEmpty(targetModel) ? "dall-e-2" : targetModel!;

        /* Unmerged change from project 'Connectors.OpenAIV2 (netstandard2.0)'
        Before:
                ClientResult<GeneratedImage> response = await RunRequestAsync(() => this.Client!.GetImageClient(targetModel).GenerateImageAsync(prompt, imageOptions, cancellationToken)).ConfigureAwait(false);
                var generatedImage = response.Value;
        After:
                ClientResult<GeneratedImage> response = await RunRequestAsync(() => this.Client!.GetImageClient((string?)targetModel).GenerateImageAsync(prompt, imageOptions, cancellationToken)).ConfigureAwait(false);
                var generatedImage = response.Value;
        */
        ClientResult<GeneratedImage> response = await RunRequestAsync(() => this.Client!.GetImageClient(targetModel).GenerateImageAsync(prompt, imageOptions, cancellationToken)).ConfigureAwait(false);
        var generatedImage = response.Value;

        return generatedImage.ImageUri?.ToString() ?? throw new KernelException("The generated image is not in url format");
    }
}
