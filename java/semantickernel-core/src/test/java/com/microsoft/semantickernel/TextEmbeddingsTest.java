// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel;

import com.azure.ai.openai.models.EmbeddingItem;
import com.azure.ai.openai.models.Embeddings;
import com.microsoft.openai.OpenAIAsyncClient;
import com.microsoft.semantickernel.ai.embeddings.EmbeddingGeneration;
import com.microsoft.semantickernel.builders.SKBuilders;
import com.microsoft.semantickernel.connectors.ai.openai.textembeddings.OpenAITextEmbeddingGeneration;
import com.microsoft.semantickernel.coreskills.TextMemorySkill;
import com.microsoft.semantickernel.e2e.AbstractKernelTest;
import com.microsoft.semantickernel.skilldefinition.ReadOnlyFunctionCollection;
import com.microsoft.semantickernel.textcompletion.CompletionSKContext;
import com.microsoft.semantickernel.textcompletion.CompletionSKFunction;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Disabled;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIf;
import org.mockito.Mockito;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import reactor.core.publisher.Mono;

import java.io.IOException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class TextEmbeddingsTest extends AbstractKernelTest {

    private static final Logger LOGGER = LoggerFactory.getLogger(TextEmbeddingsTest.class);
    private static final int EXPECTED_EMBEDDING_SIZE = 1536;

    private static OpenAIAsyncClient mockEmbeddingOpenAIAsyncClient() {
        OpenAIAsyncClient openAIAsyncClient = Mockito.mock(OpenAIAsyncClient.class);
        Embeddings embeddings = Mockito.mock(Embeddings.class);
        EmbeddingItem embeddingItem = Mockito.mock(EmbeddingItem.class);

        Mockito.when(embeddingItem.getEmbedding()).thenReturn(Collections.singletonList(1.0));

        Mockito.when(embeddings.getData()).thenReturn(Collections.singletonList(embeddingItem));

        Mockito.when(
                        openAIAsyncClient.getEmbeddings(
                                Mockito.any(String.class),
                                Mockito.argThat(
                                        it -> it != null && it.getModel().equals("text-embedding-ada-002"))))
                .thenReturn(Mono.just(embeddings));

        return openAIAsyncClient;
    }

    @Test
    @Disabled
    public void testEmbedding() {
        testEmbeddingGeneration(mockEmbeddingOpenAIAsyncClient(), 1);
    }

    @Test
    @EnabledIf("isOpenAIComTestEnabled")
    public void testEmbeddingGenerationOpenAI() throws IOException {
        testEmbeddingGeneration(getOpenAIAPI(), EXPECTED_EMBEDDING_SIZE);
    }

    @Test
    @EnabledIf("isAzureTestEnabled")
    public void testEmbeddingGenerationAzure() throws IOException {
        testEmbeddingGeneration(getAzureOpenAIAPI(), EXPECTED_EMBEDDING_SIZE);
    }

    @Test
    @EnabledIf("isAzureTestEnabled")
    public void testEmbeddingGeneration() throws IOException {
        String model = "text-embedding-ada-002";
        EmbeddingGeneration<String, Double> embeddingGeneration =
                new OpenAITextEmbeddingGeneration(getOpenAIAPI(), model);

        List<String> data = new ArrayList<>();
        data.add("This is just a test");

        LOGGER.info(String.valueOf(embeddingGeneration.generateEmbeddingsAsync(data).block()));
    }

    @Test
    @EnabledIf("isAzureTestEnabled")
    public void testMemory() throws IOException {
        String model = "text-embedding-ada-002";
        EmbeddingGeneration<String, Double> embeddingGeneration =
                new OpenAITextEmbeddingGeneration(getAzureOpenAIAPI(), model);

        Kernel kernel = buildTextEmbeddingsKernel();

        ReadOnlyFunctionCollection memory = kernel.importSkill(new TextMemorySkill(), null);

        String skPrompt =
                "\n"
                        + "ChatBot can have a conversation with you about any topic.\n"
                        + "It can give explicit instructions or say 'I don't know' if it does not have"
                        + " an answer.\n"
                        + "\n"
                        + "Information about me, from previous conversations:\n"
                        + "- {{$fact1}} {{recall $fact1}}\n"
                        + "- {{$fact2}} {{recall $fact2}}\n"
                        + "- {{$fact3}} {{recall $fact3}}\n"
                        + "- {{$fact4}} {{recall $fact4}}\n"
                        + "- {{$fact5}} {{recall $fact5}}\n"
                        + "\n"
                        + "Chat:\n"
                        + "{{$history}}\n"
                        + "User: {{$userInput}}\n"
                        + "ChatBot: ";

        Mono<CompletionSKContext> mono =
                memory.getFunction("retrieve", CompletionSKFunction.class).invokeAsync("");
        CompletionSKContext result = mono.block();

        LOGGER.info(result.getResult());
    }

    public void testEmbeddingGeneration(OpenAIAsyncClient client, int expectedEmbeddingSize) {
        String model = "text-embedding-ada-002";
        EmbeddingGeneration<String, Double> embeddingGeneration =
                new OpenAITextEmbeddingGeneration(client, model);

        List<String> data = new ArrayList<>();
        data.add("This is just");
        data.add("a test");

        embeddingGeneration
                .generateEmbeddingsAsync(data)
                .block()
                .forEach(
                        embedding -> {
                            Assertions.assertEquals(
                                    expectedEmbeddingSize, embedding.getVector().size());
                        });
    }

    private Kernel buildTextEmbeddingsKernel() throws IOException {
        String model = "text-embedding-ada-002";
        EmbeddingGeneration<String, Double> embeddingGeneration =
                new OpenAITextEmbeddingGeneration(getOpenAIAPI(), model);

        KernelConfig kernelConfig =
                SKBuilders.kernelConfig()
                        .addTextEmbeddingsGenerationService(model, kernel -> embeddingGeneration)
                        .build();

        // TODO: .WithMemoryStorage(new VolatileMemoryStore())

        // TODO: .WithMemoryStorage(new VolatileMemoryStore())

        return SKBuilders.kernel().setKernelConfig(kernelConfig).build();
    }
}
