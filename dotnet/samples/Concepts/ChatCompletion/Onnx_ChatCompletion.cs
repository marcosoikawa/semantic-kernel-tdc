﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Onnx;

namespace ChatCompletion;

// The following example shows how to use Semantic Kernel with Onnx Gen AI Chat Completion API
public class Onnx_ChatCompletion(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// Example using the service directly to get chat message content
    /// </summary>
    /// <remarks>
    /// Configuration example:
    /// <list type="table">
    /// <item>
    /// <term>ModelId:</term>
    /// <description>phi-3</description>
    /// </item>
    /// <item>
    /// <term>ModelPath:</term>
    /// <description>D:\huggingface\Phi-3-mini-4k-instruct-onnx\cpu_and_mobile\cpu-int4-rtn-block-32</description>
    /// </item>
    /// </list>
    /// </remarks>
    [Fact]
    public async Task ServicePromptAsync()
    {
        Assert.NotNull(TestConfiguration.Onnx.ModelId);   // dotnet user-secrets set "Onnx:ModelId" "<model-id>"
        Assert.NotNull(TestConfiguration.Onnx.ModelPath); // dotnet user-secrets set "Onnx:ModelPath" "<model-folder-path>"

        Console.WriteLine("======== Onnx - Chat Completion ========");

        var chatService = new OnnxRuntimeGenAIChatCompletionService(
            modelId: TestConfiguration.Onnx.ModelId,
            modelPath: TestConfiguration.Onnx.ModelPath);

        Console.WriteLine("Chat content:");
        Console.WriteLine("------------------------");

        var chatHistory = new ChatHistory("You are a librarian, expert about books");

        // First user message
        chatHistory.AddUserMessage("Hi, I'm looking for book suggestions");
        await MessageOutputAsync(chatHistory);

        // First assistant message
        var reply = await chatService.GetChatMessageContentAsync(chatHistory);
        chatHistory.Add(reply);
        await MessageOutputAsync(chatHistory);

        // Second user message
        chatHistory.AddUserMessage("I love history and philosophy, I'd like to learn something new about Greece, any suggestion");
        await MessageOutputAsync(chatHistory);

        // Second assistant message
        reply = await chatService.GetChatMessageContentAsync(chatHistory);
        chatHistory.Add(reply);
        await MessageOutputAsync(chatHistory);

        /* Output:

        Chat content:
        ------------------------
        System: You are a librarian, expert about books
        ------------------------
        User: Hi, I'm looking for book suggestions
        ------------------------
        Assistant: Sure, I'd be happy to help! What kind of books are you interested in? Fiction or non-fiction? Any particular genre?
        ------------------------
        User: I love history and philosophy, I'd like to learn something new about Greece, any suggestion?
        ------------------------
        Assistant: Great! For history and philosophy books about Greece, here are a few suggestions:

        1. "The Greeks" by H.D.F. Kitto - This is a classic book that provides an overview of ancient Greek history and culture, including their philosophy, literature, and art.

        2. "The Republic" by Plato - This is one of the most famous works of philosophy in the Western world, and it explores the nature of justice and the ideal society.

        3. "The Peloponnesian War" by Thucydides - This is a detailed account of the war between Athens and Sparta in the 5th century BCE, and it provides insight into the political and military strategies of the time.

        4. "The Iliad" by Homer - This epic poem tells the story of the Trojan War and is considered one of the greatest works of literature in the Western canon.

        5. "The Histories" by Herodotus - This is a comprehensive account of the Persian Wars and provides a wealth of information about ancient Greek culture and society.

        I hope these suggestions are helpful!
        ------------------------
        */
    }

    /// <summary>
    /// Example using the kernel to send a chat history and get a chat message content
    /// </summary>
    /// <remarks>
    /// Configuration example:
    /// <list type="table">
    /// <item>
    /// <term>ModelId:</term>
    /// <description>phi-3</description>
    /// </item>
    /// <item>
    /// <term>ModelPath:</term>
    /// <description>D:\huggingface\Phi-3-mini-4k-instruct-onnx\cpu_and_mobile\cpu-int4-rtn-block-32</description>
    /// </item>
    /// </list>
    /// </remarks>
    [Fact]
    public async Task ChatPromptAsync()
    {
        Assert.NotNull(TestConfiguration.Onnx.ModelId);   // dotnet user-secrets set "Onnx:ModelId" "<model-id>"
        Assert.NotNull(TestConfiguration.Onnx.ModelPath); // dotnet user-secrets set "Onnx:ModelPath" "<model-folder-path>"

        Console.WriteLine("======== Onnx - Chat Prompt Completion ========");

        StringBuilder chatPrompt = new("""
                                       <message role="system">You are a librarian, expert about books</message>
                                       <message role="user">Hi, I'm looking for book suggestions</message>
                                       """);

        var kernel = Kernel.CreateBuilder()
            .AddOnnxRuntimeGenAIChatCompletion(
                modelId: TestConfiguration.Onnx.ModelId,
                modelPath: TestConfiguration.Onnx.ModelPath)
            .Build();

        var reply = await kernel.InvokePromptAsync(chatPrompt.ToString());

        chatPrompt.AppendLine($"<message role=\"assistant\"><![CDATA[{reply}]]></message>");
        chatPrompt.AppendLine("<message role=\"user\">I love history and philosophy, I'd like to learn something new about Greece, any suggestion</message>");

        reply = await kernel.InvokePromptAsync(chatPrompt.ToString());

        Console.WriteLine(reply);
    }

    /// <summary>
    /// Outputs the last message of the chat history
    /// </summary>
    private Task MessageOutputAsync(ChatHistory chatHistory)
    {
        var message = chatHistory.Last();

        Console.WriteLine($"{message.Role}: {message.Content}");
        Console.WriteLine("------------------------");

        return Task.CompletedTask;
    }
}
