﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Examples;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Xunit;
using Xunit.Abstractions;

namespace KernelSyntaxExamples;

// This example shows how to use different Handlebars syntax options like:
//  - set (works)
//  - function calling (works)
//  - loops (each) - not working at the moment, but tries to do so over a complex object generated by one of the functions: a JSON array-
//  - array - not working at the moment - to accumulate the results of the loop in an array
//  - conditionals (works)
//  - concatenation (works)
// In order to create a Prompt Function that fully benefits from the Handlebars syntax power.
// The example also shows how to use the HandlebarsPlanner to generate a plan (and persist it) which was used to generate the initial Handlebar template.
// The example also shows how to create two prompt functions and a plugin to group them together.
public class Example77_HandlebarsPromptSyntax : BaseTest
{
    private static readonly string s_companyDescription = "The company is a startup that is building new AI solutions for the market. using Generative AI and AI orchestration novel technologies. The company is an expert on this recently launched SDK (Software Development Toolkit) named Semantic Kernel. Semantic Kernel or SK, enables AI Orchestration with .NET which is production ready, enterprise ready and cloud ready." +
            "Also it is able to self plan and execute complex tasks and use the power of AI agents which" +
            "enables to divide-and-conquer complex problems between different entities that specialize in " +
            "concrete tasks like for example project management, coding and creating tests as well as other" +
            " agents can be responsible for executing the tests and assessing the code delivered and iterate" +
            " - this means creating feedback loops until the quality levels are met. The company is thinking of using AI Agent programming on coding, writing and project planning, and anything where AI Agents" +
            " can be applied and revolutionize a process or market niche.";
    public KernelFunction KernelFunctionGenerateProductNames{ get; set; }
    public KernelFunction KernelFunctionGenerateProductDescription { get; set; }

    [Fact]
    public async Task RunAsync()
    {
        this.WriteLine("======== Handlebars Prompt Syntax Sample ========");

        string openAIModelId = TestConfiguration.OpenAI.ChatModelId;
        string openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (openAIApiKey == null)
        {
            this.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        if (openAIModelId == null)
        {
            this.WriteLine("openAIModelId credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: openAIModelId,
                apiKey: openAIApiKey)
            .Build();

        KernelPlugin productMagicianPlugin = GenerateProductMagicianPlugin();
        kernel.Plugins.Add(productMagicianPlugin);

        await TestProductFunctionsAsync(kernel);

        await RunHandlebarsPlannerSampleAsync(kernel);

        await RunHandlebarsTemplateSample01Async(kernel);

        await RunHandlebarsTemplateSample02Async(kernel);

        await RunHandlebarsTemplateSample03Async(kernel);
    }

    private async Task RunHandlebarsTemplateSample03Async(Kernel kernel)
    {
        string handlebarsTemplate3 = @"
            {{!-- example of set with input and function calling with two syntax types --}}
            {{set ""companyDescription"" input}}
            {{set ""productNames"" (productMagician-GenerateJSONProducts companyDescription)}}

            {{#if generateEngagingDescriptions}} 
                {{!-- Step 2: Create array for storing final descriptions --}}
                {{set ""finalDescriptions"" (array)}}

                {{!-- Step 3: Iterate over each generated product name --}}
                {{#each productNames}}
                    {{#each this}}
                      {{!-- Step 3.1: Concatenating productName to initial company description --}}
                      {{set ""productDescription"" (concat ""Product Name: "" this.name "" Description: "" this.description)}}

                      {{!-- Step 3.2: Generate compelling description for each productName --}}
                      {{set ""compellingDescription"" (productMagician-GenerateProductCompellingDescription productDescription)}}

                      {{!-- Step 3.3: Concatenate compelling description and product number --}}                  
                      {{set ""outputDescription"" (concat ""PRODUCT :"" this.name "" Engaging Description: "" compellingDescription)}}

                      {{!-- Step 3.4: Add output description to the list --}}
                      {{set ""finalDescriptions"" (array finalDescriptions outputDescription)}}
                      {{set ""finalDescriptionsV2"" (concat finalDescriptionsV2 "" -- "" outputDescription)}}
                    {{/each}}
                {{/each}}

                {{!-- Step 4: Print all product names and compelling descriptions --}}
                OUTPUT The following product descriptions as is, do not modify anything:
                {{json finalDescriptionsV2}}
    
            {{else}} 
                {{!-- Example of concatenating text and variables to finally output it with json --}}
                {{set ""finalOutput"" (concat ""Description 1: "" productNames "" Description 2: "" productNames2)}}
                {{json finalOutput}}
            {{/if}}";

        await ExecuteHandlebarsPromptAsync(kernel, s_companyDescription, handlebarsTemplate3);
    }

    private async Task RunHandlebarsTemplateSample02Async(Kernel kernel)
    {
        string handlebarsTemplate2 = @"
            {{!-- example of set with input and function calling with two syntax types --}}
            {{set ""companyDescription"" input}}
            {{set ""productNames"" (productMagician-GenerateJSONProducts companyDescription)}}
            {{json productNames}}

            {{set ""finalDescriptionsV2"" ""- PRODUCTS AND ENGAGING DESCRIPTIONS -""}}

            {{!-- Step 3: Iterate over each generated product name --}}
            {{#each productNames}}
                {{#each this}}
                    {{!-- Step 3.1: Concatenating productName to initial company description --}}
                    {{set ""productDescription"" (concat ""Product Name: "" this.name "" Description: "" this.description)}}
                    {{json productDescription}}

                    {{!-- Step 3.4: Add output description to the list --}}
                    {{set ""finalDescriptionsV2"" (concat finalDescriptionsV2 "" -- "" productDescription)}}
                {{/each}}
            {{/each}}

            {{!-- Step 4: Print all product names and compelling descriptions --}}
            OUTPUT The following product descriptions as is, do not modify anything:
            {{json finalDescriptionsV2}}
         ";
        await ExecuteHandlebarsPromptAsync(kernel, s_companyDescription, handlebarsTemplate2);
    }

    private async Task RunHandlebarsTemplateSample01Async(Kernel kernel)
    {
        string handlebarsTemplate01 = @"
            {{!-- example of set with input and function calling with two syntax types --}}
            {{set ""companyDescription"" input}}
            {{set ""productNames"" (productMagician-GenerateJSONProducts companyDescription)}}

            {{set ""output"" (concat ""Company description: "" companyDescription "" product Names: "" productNames)}}
            {{json output}}";

        await ExecuteHandlebarsPromptAsync(kernel, s_companyDescription, handlebarsTemplate01);
    }

    private async Task RunHandlebarsPlannerSampleAsync(Kernel kernel)
    {
        // Using the planner to generate a plan for the user
        string userPrompt =
            "Using as input the following company description:" +
            "---" +
            " {{input}}" +
            "---" +
            "I want to generate five product names and engaging descriptions for a company." +
            "Please provide your output as a JSON array of products, where each product contains a name and description." +
            "For example:" +
            "---" +
            "{\r\n\"products\": [\r\n    {\r\n        \"name\": \"SmartCode SK\",\r\n        \"description\": \"An AI solution that utilizes AI agent programming to automate code writing and assess the quality of code, reducing the need for manual review and increasing code efficiency\"\r\n    },\r\n    {\r\n        \"name\": \"ProjectMind SK\",\r\n        \"description\": \"An AI-powered project management tool that utilizes Semantic Kernel to automate project planning, task scheduling, and enable more effective communication within teams\"\r\n    },\r\n    {\r\n        \"name\": \"SK WriterPlus\",\r\n        \"description\": \"An AI-driven writing solution that uses AI Agents and Semantic Kernel technology to automate content creation, editing, and proofreading tasks\"\r\n    },\r\n    {\r\n        \"name\": \"SQA Tester SK\",\r\n        \"description\": \"A software product that utilizes AI agents to execute tests, assess the code delivered and iterate. It uses a divide-and-conquer approach to simplify complex testing problems\"\r\n    },\r\n    {\r\n        \"name\": \"SK CloudMaster\",\r\n        \"description\": \"An AI solution that intelligently manages and orchestrates cloud resources using the power of AI agents and Semantic Kernel technology, ready for enterprise and production environments\"\r\n    }\r\n]\r\n}" +
            "---" +
            "generate the compelling, engaging, description" +
            "Please while doing this provide all the information as input:" +
            "For point 2, concatenate the product name to the rough description." +
            "Please use the format Product name: productname Description: description substituting" +
            "productname and description by the product names provided on point 1" +
            "Finally output all the product names and engaging descriptions preceded by PRODUCT 1: for the first" +
            "PRODUCT 2: for the second, and so on.";

        // Create the plan
        var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions() { AllowLoops = true });
        var plan = await planner.CreatePlanAsync(kernel, userPrompt);
        var planName = "plan4ProductsGeneration.txt";

        // Print the plan to the console
        WriteLine($"Plan: {plan}");

        // Serialize the plan to a string and save it to a file
        var serializedPlan = plan.ToString();
        await File.WriteAllTextAsync(planName, serializedPlan);

        // Deserialize the plan from the file and create a new plan
        string retrievedPlan = await File.ReadAllTextAsync(planName);
        plan = new HandlebarsPlan(serializedPlan);

        // Execute the plan
        // Commented due to issues on the generation and execution of the Handlebars Plan
        // Generated the following issues:
        // https://github.com/microsoft/semantic-kernel/issues/4893
        // https://github.com/microsoft/semantic-kernel/issues/4894
        // https://github.com/microsoft/semantic-kernel/issues/4895
        //var result = await plan.InvokeAsync(kernel);
        //WriteLine($"\nResult:\n{result}\n");
    }

    private async Task TestProductFunctionsAsync(Kernel kernel)
    {
        var productsResult =
            await kernel.InvokeAsync(KernelFunctionGenerateProductNames,
            new() {
              { "input", s_companyDescription }
            });

        WriteLine($"Testing Products generation prompt");
        WriteLine($"Result: {productsResult}");

        string ProductDescription = "Product name: Skynet SDK Product description:A powerful .NET SDK destined to empower developers with advanced AI orchestration capabilities, capable of handling complex task automation.";
        var productDescriptionResult =
            await kernel.InvokeAsync(KernelFunctionGenerateProductDescription,
            new() {
              { "input", ProductDescription }
            });

        WriteLine($"Testing Products Description Generation Prompt");
        WriteLine($"Result: {productDescriptionResult}");
    }

    private KernelPlugin GenerateProductMagicianPlugin()
    {
        this.KernelFunctionGenerateProductNames =
            KernelFunctionFactory.CreateFromPrompt(
                "Given the company description, generate five different product names in name and with a function " +
                "that match the company description. " +
                "Ensure that they match the company and are aligned to it. " +
                "Think of products this company would really make and also have market potential. " +
                "Be original and do not make too long names or use more than 3-4 words for them." +
                "Also, the product name should be catchy and easy to remember. " +
                "Output the product names in a JSON array inside a JSON object named products. " +
                "On them use the name and description as keys." +
                "Ensure the JSON is well formed and is valid" +
                "The company description: " +
                "---" +
                "{{$input}} " +
                "---" +
                "AGAIN ENSURE YOU FOLLOW THE DESCRIBED JSON FORMAT " +
                "IF YOU FOLLOW IT WELL I WILL PRAISE YOU " +
                "AND GIVE YOU A BONUS!!!" +
                "The JSON format should look like this:" +
                "---" +
                "{\r\n\"products\": [\r\n    {\r\n        \"name\": \"SmartCode SK\",\r\n        \"description\": \"An AI solution that utilizes AI agent programming to automate code writing and assess the quality of code, reducing the need for manual review and increasing code efficiency\"\r\n    },\r\n    {\r\n        \"name\": \"ProjectMind SK\",\r\n        \"description\": \"An AI-powered project management tool that utilizes Semantic Kernel to automate project planning, task scheduling, and enable more effective communication within teams\"\r\n    },\r\n    {\r\n        \"name\": \"SK WriterPlus\",\r\n        \"description\": \"An AI-driven writing solution that uses AI Agents and Semantic Kernel technology to automate content creation, editing, and proofreading tasks\"\r\n    },\r\n    {\r\n        \"name\": \"SQA Tester SK\",\r\n        \"description\": \"A software product that utilizes AI agents to execute tests, assess the code delivered and iterate. It uses a divide-and-conquer approach to simplify complex testing problems\"\r\n    },\r\n    {\r\n        \"name\": \"SK CloudMaster\",\r\n        \"description\": \"An AI solution that intelligently manages and orchestrates cloud resources using the power of AI agents and Semantic Kernel technology, ready for enterprise and production environments\"\r\n    }\r\n]\r\n}" +
                 "---",
                functionName: "GenerateJSONProducts",
                description: "Generate a JSON with five unique product objects, every object containing a unique name and short description matching company .");


        this.KernelFunctionGenerateProductDescription =
            KernelFunctionFactory.CreateFromPrompt(
                "Given a product name and initial description, generate a description which is compelling, " +
                "engaging and stunning. Also add at the end how would you approach to develop, create it," +
                "with Semantic Kernel." +
                "Think of marketing terms and use positive words but do not lie and oversell. " +
                "Be original and do not make a too long description or use more than 2 paragraphs for it." +
                "Also, the product description should be catchy and easy to remember. " +
                "Output the description followed by the development approach preceded by Development approach: " +
                "The product name and description: {{$input}}",
                functionName: "GenerateProductCompellingDescription",
                description: "Generate a compelling product description for a product name and initial description.");

        KernelPlugin productMagicianPlugin =
            KernelPluginFactory.CreateFromFunctions(
                "productMagician",
                "Helps create a set of products for a company and descriptions for them.",
                new[] {
                    this.KernelFunctionGenerateProductNames,
                    this.KernelFunctionGenerateProductDescription
                });

        return productMagicianPlugin;
    }

    private async Task ExecuteHandlebarsPromptAsync(Kernel kernel, string companyDescription, string handlebarsTemplate)
    {
        var HandlebarsSPromptFunction = kernel.CreateFunctionFromPrompt(
            new()
            {
                Template = handlebarsTemplate,
                TemplateFormat = "handlebars"
            },
            new HandlebarsPromptTemplateFactory()
        );

        // Invoke prompt
        var customHandlebarsPromptResult = await kernel.InvokeAsync(
                    HandlebarsSPromptFunction,
                    new() {
                        { "input", companyDescription },
                        { "generateEngagingDescriptions", true }
                    }
                );

        Console.WriteLine($"Result:  {customHandlebarsPromptResult}");
    }

    public Example77_HandlebarsPromptSyntax(ITestOutputHelper output) : base(output)
    {
    }
}
