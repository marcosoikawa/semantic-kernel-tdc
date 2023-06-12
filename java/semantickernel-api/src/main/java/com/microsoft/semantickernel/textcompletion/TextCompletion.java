// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.textcompletion;

import java.util.List;

import com.azure.ai.openai.OpenAIAsyncClient;

import reactor.core.publisher.Mono;

/** Interface for text completion services */
public interface TextCompletion {
    // TODO: Support Cancellation Token

    /**
     * Creates a completion for the prompt and settings.
     *
     * @param text            The prompt to complete.
     * @param requestSettings Request settings for the completion API
     * @return Text generated by the remote model
     */
    Mono<List<String>> completeAsync(String text, CompletionRequestSettings requestSettings);

    abstract class Builder {
        protected Builder() {
        }

        public abstract TextCompletion build(OpenAIAsyncClient client, String modelId);
    }
}
