﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.UnitTests.CoreSkills;

public class PlannerSkillTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private const string FunctionFlowRunnerText = @"
<goal>
Solve the equation x^2 = 2.
</goal>
<plan>
  <function.math.simplify input=""x^2 = 2"" />
</plan>
";

    private const string GoalText = "Solve the equation x^2 = 2.";

    public PlannerSkillTests(ITestOutputHelper testOutputHelper)
    {
        this._testOutputHelper = testOutputHelper;
        this._testOutputHelper.WriteLine("Tests initialized");
    }

    [Fact]
    public void ItCanBeInstantiated()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);

        // Act - Assert no exception occurs
        _ = new PlannerSkill(kernel);
    }

    [Fact]
    public void ItCanBeImported()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);

        // Act - Assert no exception occurs e.g. due to reflection
        _ = kernel.ImportSkill(new PlannerSkill(kernel), "planner");
    }

    // TODO: fix the tests - they are not mocking the AI connector and should have been failing.
    //       Looks like they've been passing only because the planner is swallowing HTTP errors.
    // [Fact]
    // public async Task ItCanCreatePlanAsync()
    // {
    //     // Arrange
    //     var kernel = KernelBuilder.Create();
    //     var factory = new Mock<Func<IKernel, ITextCompletion>>();
    //     kernel.Config.AddTextCompletion("test", factory.Object, true);
    //     var plannerSkill = new PlannerSkill(kernel);
    //     var planner = kernel.ImportSkill(plannerSkill, "planner");
    //
    //     // Act
    //     var context = await kernel.RunAsync(GoalText, planner["CreatePlan"]);
    //
    //     // Assert
    //     var plan = context.Variables.ToPlan();
    //     Assert.NotNull(plan);
    //     Assert.NotNull(plan.Id);
    //     Assert.Equal(GoalText, plan.Goal);
    //     Assert.StartsWith("<goal>\nSolve the equation x^2 = 2.\n</goal>", plan.PlanString, StringComparison.OrdinalIgnoreCase);
    // }

    [Fact]
    public async Task CreatePlanWhenAchievableAndFunctionsExistsShouldBeValidAsync()
    {
        var kernelMock = this.CreateKernelMock(out _, out var skillCollectionMock, out _);
        var returnVerifyContext = this.CreateSKContext(kernelMock.Object);
        returnVerifyContext.Variables.Update("{\"valid\": true}");
        var mockFunctionVerify = SetupMockFunction("Goal", returnVerifyContext);

        var returnCreatePlanContext = this.CreateSKContext(kernelMock.Object);
        returnCreatePlanContext.Variables.Update("<plan></plan>");
        var mockFunctionCreatePlan = SetupMockFunction("Goal", returnCreatePlanContext);

        kernelMock.Setup(k => k.RegisterSemanticFunction(It.IsAny<string>(), It.IsIn<string>("CreatePlanAsync"), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunctionCreatePlan.Object);

        kernelMock.Setup(k => k.RegisterSemanticFunction(It.IsAny<string>(), It.IsIn<string>("VerifyCanCreatePlanForGoalAsync"), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunctionVerify.Object);

        skillCollectionMock.Setup(sc => sc.GetFunctionsView(It.IsAny<bool>(), It.IsAny<bool>())).Returns(new FunctionsView());

        var target = new PlannerSkill(kernelMock.Object);

        // Act
        var context = this.CreateSKContext(kernelMock.Object);
        var result = await target.CreatePlanAsync("Goal", context);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
    }

    [Theory]
    [InlineData("\"Not possible to complete the plan\"", "InvalidPlan: Not possible to complete the plan")]
    [InlineData("\"No reason\"", "InvalidPlan: No reason")]
    [InlineData("\"\"", "InvalidPlan: No reason was provided")]
    [InlineData("null", "InvalidPlan: No reason was provided")]
    [InlineData("\" \"", "InvalidPlan: No reason was provided")]
    public async Task CreatePlanWhenNotAchievableShouldBeInvalidAsync(string llmReason, string expectedReason)
    {
        var kernelMock = this.CreateKernelMock(out _, out var skillCollectionMock, out _);
        var returnVerifyContext = this.CreateSKContext(kernelMock.Object);
        returnVerifyContext.Variables.Update($"{{\"valid\": false, \"reason\": {llmReason} }}");
        var mockFunctionVerify = SetupMockFunction("Goal", returnVerifyContext);

        kernelMock.Setup(k =>
                k.RegisterSemanticFunction(It.IsAny<string>(), It.IsIn<string>("VerifyCanCreatePlanForGoalAsync"), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunctionVerify.Object);

        skillCollectionMock.Setup(sc => sc.GetFunctionsView(It.IsAny<bool>(), It.IsAny<bool>())).Returns(new FunctionsView());

        var target = new PlannerSkill(kernelMock.Object);

        // Act
        var result = await target.CreatePlanAsync("Goal", this.CreateSKContext(kernelMock.Object));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ErrorOccurred);
        Assert.Equal(expectedReason, result.LastErrorDescription);
    }

    [Fact]
    public async Task CreatePlanWhenAchievableAndFunctionsDontExistsShouldBeInvalidAsync()
    {
        var kernelMock = this.CreateKernelMock(out _, out var skillCollectionMock, out _);
        var returnVerifyContext = this.CreateSKContext(kernelMock.Object);
        returnVerifyContext.Variables.Update("{\"valid\": true}");
        var mockFunctionVerify = SetupMockFunction("Goal", returnVerifyContext);

        var returnCreatePlanContext = this.CreateSKContext(kernelMock.Object);
        returnCreatePlanContext.Variables.Update("<plan><function.SkillNotExists.FunctionNotExists/></plan>");
        var mockFunctionCreatePlan = SetupMockFunction("Goal", returnCreatePlanContext);

        kernelMock.Setup(k => k.RegisterSemanticFunction(It.IsAny<string>(), It.IsIn<string>("CreatePlanAsync"), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunctionCreatePlan.Object);

        kernelMock.Setup(k =>
                k.RegisterSemanticFunction(It.IsAny<string>(), It.IsIn<string>("VerifyCanCreatePlanForGoalAsync"), It.IsAny<SemanticFunctionConfig>()))
            .Returns(mockFunctionVerify.Object);

        skillCollectionMock.Setup(sc => sc.GetFunctionsView(It.IsAny<bool>(), It.IsAny<bool>())).Returns(new FunctionsView());

        var target = new PlannerSkill(kernelMock.Object);

        // Act
        var context = this.CreateSKContext(kernelMock.Object);
        var result = await target.CreatePlanAsync("Goal", context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ErrorOccurred);
    }

    [Fact]
    public async Task ItCanExecutePlanTextAsync()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));

        // Act
        var context = await kernel.RunAsync(FunctionFlowRunnerText, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);

        // Since not using SkillPlan or PlanExecution object, this won't be present.
        // Maybe we do work to parse this out. Not doing too much though since we might move to json instead of xml.
        // Assert.Equal(GoalText, plan.Goal);
    }

    [Fact]
    public async Task ItCanExecutePlanAsync()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        SkillPlan createdPlan = new()
        {
            Goal = GoalText,
            PlanString = FunctionFlowRunnerText
        };

        // Act
        var variables = new ContextVariables();
        _ = variables.UpdateWithPlanEntry(createdPlan);
        var context = await kernel.RunAsync(variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(GoalText, plan.Goal);
    }

    // TODO: fix the tests - they are not mocking the AI connector and should have been failing.
    //       Looks like they've been passing only because the planner is swallowing HTTP errors.
    // [Fact]
    // public async Task ItCanCreateSkillPlanAsync()
    // {
    //     // Arrange
    //     var kernel = KernelBuilder.Create();
    //     var factory = new Mock<Func<IKernel, ITextCompletion>>();
    //     kernel.Config.AddTextCompletion("test", factory.Object, true);
    //     var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
    //
    //     // Act
    //     var context = await kernel.RunAsync(GoalText, plannerSkill["CreatePlan"]);
    //
    //     // Assert
    //     var plan = context.Variables.ToPlan();
    //     Assert.NotNull(plan);
    //     Assert.NotNull(plan.Id);
    //     Assert.Equal(GoalText, plan.Goal);
    //     Assert.StartsWith("<goal>\nSolve the equation x^2 = 2.\n</goal>", plan.PlanString, StringComparison.OrdinalIgnoreCase);
    // }

    [Fact]
    public async Task ItCanExecutePlanJsonAsync()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        SkillPlan createdPlan = new()
        {
            Goal = GoalText,
            PlanString = FunctionFlowRunnerText
        };

        // Act
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(GoalText, plan.Goal);
    }

    [Fact]
    public async Task NoGoalExecutePlanReturnsInvalidResultAsync()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));

        // Act
        var context = await kernel.RunAsync(GoalText, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(string.Empty, plan.Goal);
        Assert.Equal(GoalText, plan.PlanString);
        Assert.False(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Contains("No goal found.", plan.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidPlanExecutePlanReturnsInvalidResultAsync()
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));

        // Act
        var context = await kernel.RunAsync("<someTag>" + GoalText, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(string.Empty, plan.Goal);
        Assert.Equal("<someTag>" + GoalText, plan.PlanString);
        Assert.False(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Contains("Failed to parse plan xml.", plan.Result, StringComparison.OrdinalIgnoreCase);
    }

    //
    // Advanced tests for ExecutePlan that use mock sk functions to test the flow
    //
    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
</plan>")]
    public async Task ExecutePlanCanCallFunctionAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Hello World", plan.Result, true);
    }

    // Test that contains a #text node in the plan
    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
This is some text
</plan>")]
    public async Task ExecutePlanCanCallFunctionWithTextAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Hello World", plan.Result, true);
    }

    // use SplitInput after Echo
    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
<function.MockSkill.SplitInput />
<function.MockSkill.Echo input=""$Second"" />
<function.MockSkill.Echo input=""$First"" />
</plan>")]
    public async Task ExecutePlanCanCallFunctionWithVariablesAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act - run the plan 4 times to run all steps
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Echo Result", plan.Result, true);
    }

    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
<function.MockSkill.SplitInput />
<function.MockSkill.Echo input=""$Second"" setContextVariable=""ECHO_SECOND""/>
<function.MockSkill.Echo input=""$First"" setContextVariable=""ECHO_FIRST""/>
<function.MockSkill.Echo input=""$ECHO_FIRST"" appendToResult=""RESULT__1""/>
<function.MockSkill.Echo input=""$ECHO_SECOND"" appendToResult=""RESULT__2""/>
</plan>")]
    public async Task ExecutePlanCanCallFunctionWithVariablesAndResultAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act - run the plan 6 times to run all steps
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        var separator = Environment.NewLine + Environment.NewLine;
        Assert.Equal($"RESULT__1{separator}Echo Result: Echo Result: Echo Result{separator}RESULT__2{separator}Echo Result: Echo Result:  Hello World",
            plan.Result, true);
    }

    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
<function.MockSkill.SplitInput />
<function.MockSkill.Echo input=""$Second"" setContextVariable=""ECHO_SECOND""/>
<function.MockSkill.Echo input=""$First"" setContextVariable=""ECHO_FIRST""/>
<function.MockSkill.Echo input=""$ECHO_SECOND;$ECHO_FIRST"" />
</plan>")]
    public async Task ExecutePlanCanCallFunctionWithChainedVariablesAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act - run the plan 5 times to run all steps
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Echo Result:  Hello WorldEcho Result: Echo Result", plan.Result, true);
    }

    // test that a <tag> that is not <function> will just get skipped
    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World"" />
<tag>Some other tag</tag>
<function.MockSkill.Echo />
</plan>")]
    public async Task ExecutePlanCanSkipTagsAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act - run the plan 2 times to run all steps
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Echo Result: Hello World", plan.Result, true);
    }

    [Theory]
    [InlineData("Test the functionFlowRunner", @"<goal>Test the functionFlowRunner</goal>
<plan>
<function.MockSkill.Echo input=""Hello World""/>
<function.MockSkill.Echo input=""$output""/>
</plan>")]
    public async Task ExecutePlanCanSkipOutputAsync(string goalText, string planText)
    {
        // Arrange
        var kernel = KernelBuilder.Create();
        var factory = new Mock<Func<IKernel, ITextCompletion>>();
        kernel.Config.AddTextCompletionService("test", factory.Object, true);
        var plannerSkill = kernel.ImportSkill(new PlannerSkill(kernel));
        _ = kernel.ImportSkill(new MockSkill(this._testOutputHelper), "MockSkill");
        SkillPlan createdPlan = new()
        {
            Goal = goalText,
            PlanString = planText
        };

        // Act - run the plan 2 times to run all steps
        var context = await kernel.RunAsync(createdPlan.ToJson(), plannerSkill["ExecutePlan"]);
        context = await kernel.RunAsync(context.Variables, plannerSkill["ExecutePlan"]);

        // Assert
        var plan = context.Variables.ToPlan();
        Assert.NotNull(plan);
        Assert.NotNull(plan.Id);
        Assert.Equal(goalText, plan.Goal);
        Assert.True(plan.IsSuccessful);
        Assert.True(plan.IsComplete);
        Assert.Equal("Echo Result: Echo Result: Hello World", plan.Result, true);
    }

    private SKContext CreateSKContext(
        IKernel kernel,
        ContextVariables? variables = null,
        CancellationToken cancellationToken = default)
    {
        return new SKContext(variables ?? new ContextVariables(), kernel.Memory, kernel.Skills, kernel.Log, cancellationToken);
    }

    private Mock<IKernel> CreateKernelMock(
        out Mock<ISemanticTextMemory> semanticMemoryMock,
        out Mock<IReadOnlySkillCollection> mockSkillCollection,
        out Mock<ILogger> mockLogger)
    {
        semanticMemoryMock = new Mock<ISemanticTextMemory>();
        mockSkillCollection = new Mock<IReadOnlySkillCollection>();
        mockLogger = new Mock<ILogger>();

        var kernelMock = new Mock<IKernel>();
        kernelMock.SetupGet(k => k.Skills).Returns(mockSkillCollection.Object);
        kernelMock.SetupGet(k => k.Log).Returns(mockLogger.Object);
        kernelMock.SetupGet(k => k.Memory).Returns(semanticMemoryMock.Object);

        return kernelMock;
    }

    private static Mock<ISKFunction> SetupMockFunction(string promptStartsWith, SKContext expectedReturnContext)
    {
        var mockFunction = new Mock<ISKFunction>();

        mockFunction.Setup(f => f.InvokeAsync(It.Is<string>(i => i.StartsWith(promptStartsWith)),
                It.IsAny<SKContext?>(), It.IsAny<CompleteRequestSettings?>(),
                It.IsAny<ILogger?>(),
                It.IsAny<CancellationToken?>()))
            .ReturnsAsync(expectedReturnContext);

        mockFunction.Setup(f => f.InvokeAsync(It.Is<SKContext>(i => i.Variables.Input.StartsWith(promptStartsWith)),
                It.IsAny<CompleteRequestSettings?>(),
                It.IsAny<ILogger?>(),
                It.IsAny<CancellationToken?>()))
            .ReturnsAsync(expectedReturnContext);

        return mockFunction;
    }

    public class MockSkill
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MockSkill(ITestOutputHelper testOutputHelper)
        {
            this._testOutputHelper = testOutputHelper;
        }

        [SKFunction("Split the input into two parts")]
        [SKFunctionName("SplitInput")]
        [SKFunctionInput(Description = "The input text to split")]
        public Task<SKContext> SplitInputAsync(string input, SKContext context)
        {
            var parts = input.Split(':');
            context.Variables.Set("First", parts[0]);
            context.Variables.Set("Second", parts[1]);
            return Task.FromResult(context);
        }

        [SKFunction("Echo the input text")]
        [SKFunctionName("Echo")]
        public Task<SKContext> EchoAsync(string text, SKContext context)
        {
            this._testOutputHelper.WriteLine(text);
            _ = context.Variables.Update("Echo Result: " + text);
            return Task.FromResult(context);
        }
    }
}
