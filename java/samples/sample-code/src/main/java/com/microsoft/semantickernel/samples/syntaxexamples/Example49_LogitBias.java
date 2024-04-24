// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.samples.syntaxexamples;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.azure.ai.openai.OpenAIClientBuilder;
import com.azure.core.credential.AzureKeyCredential;
import com.azure.core.credential.KeyCredential;
import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.aiservices.openai.chatcompletion.OpenAIChatCompletion;
import com.microsoft.semantickernel.orchestration.InvocationContext;
import com.microsoft.semantickernel.orchestration.PromptExecutionSettings;
import com.microsoft.semantickernel.services.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.services.chatcompletion.ChatHistory;
import java.util.Arrays;
import java.util.HashMap;
import java.util.Map;

/**
 * Logit_bias is an optional parameter that modifies the likelihood of specified tokens appearing in
 * a Completion. When using the Token Selection Biases parameter, the bias is added to the logits
 * generated by the model prior to sampling.
 */
public class Example49_LogitBias {

    private static final String CLIENT_KEY = System.getenv("CLIENT_KEY");
    private static final String AZURE_CLIENT_KEY = System.getenv("AZURE_CLIENT_KEY");

    // Only required if AZURE_CLIENT_KEY is set
    private static final String CLIENT_ENDPOINT = System.getenv("CLIENT_ENDPOINT");

    public static void main(String[] args) {

        OpenAIAsyncClient client;

        if (AZURE_CLIENT_KEY != null) {
            client = new OpenAIClientBuilder()
                .credential(new AzureKeyCredential(AZURE_CLIENT_KEY))
                .endpoint(CLIENT_ENDPOINT)
                .buildAsyncClient();

        } else {
            client = new OpenAIClientBuilder()
                .credential(new KeyCredential(CLIENT_KEY))
                .buildAsyncClient();
        }

        ChatCompletionService openAIChatCompletion = OpenAIChatCompletion.builder()
            .withOpenAIAsyncClient(client)
            .withModelId("gpt-35-turbo-2")
            .build();

        Kernel kernel = Kernel.builder()
            .withAIService(ChatCompletionService.class, openAIChatCompletion)
            .build();

        // To use Logit Bias you will need to know the token ids of the words you want to use.
        // Getting the token ids using the GPT Tokenizer: https://platform.openai.com/tokenizer

        // The following text is the tokenized version of the book related tokens
        // "novel literature reading author library story chapter paperback hardcover ebook publishing fiction nonfiction manuscript textbook bestseller bookstore reading list bookworm"
        var keys = new int[] { 39142, 301, 17649, 5403, 3229, 6875, 3446, 12735, 86831, 2653, 3773,
                35097, 23763, 17422, 2536, 58162, 47913, 56185, 1888, 35199, 79761, 5403, 1160,
                2363,
                56741 };
        /*
         * // If using GPT-3 (not GPT-3.5 or GPT-4)
         * var keys = new int[]{3919, 626, 9285, 3555, 1772, 5888, 1621, 6843, 46771, 1327, 9631,
         * 47179, 12407, 10165, 1729, 24046, 17116, 28979, 1266, 32932, 44346, 3555, 1351, 1492,
         * 25323};
         */
        // This will make the model try its best to avoid any of the above related words.
        //-100 to potentially ban all the tokens from the list.
        Map<Integer, Integer> biases = new HashMap<>();
        Arrays.stream(keys)
            .asLongStream()
            .forEach(key -> biases.put((int) key, -100));

        var settings = PromptExecutionSettings.builder()
            .withTokenSelectionBiases(biases)
            .build();

        var invocationContext = InvocationContext.builder().withPromptExecutionSettings(settings)
            .build();

        System.out.println("Chat content:");
        System.out.println("------------------------");

        var chatHistory = new ChatHistory("You are a librarian expert");

        // First user message
        chatHistory.addUserMessage("Hi, I'm looking some suggestions");
        messageOutputAsync(chatHistory);

        var replyMessage = openAIChatCompletion.getChatMessageContentsAsync(chatHistory,
            kernel, invocationContext).block();
        replyMessage.forEach(message -> chatHistory.addAssistantMessage(message.getContent()));
        messageOutputAsync(chatHistory);

        chatHistory.addUserMessage(
            "I love history and philosophy, I'd like to learn something new about Greece, any suggestion");
        messageOutputAsync(chatHistory);

        replyMessage = openAIChatCompletion.getChatMessageContentsAsync(chatHistory,
            kernel, invocationContext).block();
        replyMessage.forEach(message -> chatHistory.addAssistantMessage(message.getContent()));
        messageOutputAsync(chatHistory);

    }

    private static void messageOutputAsync(ChatHistory chatHistory) {
        var message = chatHistory.getLastMessage();

        System.out.println(message.get().getAuthorRole() + ": " + message.get().getContent());
        System.out.println("------------------------");
    }

}
