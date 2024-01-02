﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

#pragma warning disable CA1812 // Uninstantiated internal types

namespace SemanticKernel.IntegrationTests.Fakes;

internal sealed class ThrowingEmailPluginFake
{
    [KernelFunction, Description("Given an email address and message body, send an email")]
    public Task<string> SendEmailAsync(
        [Description("The body of the email message to send.")] string input = "",
        [Description("The email address to send email to.")] string? email_address = "default@email.com")
    {
        throw new ArgumentException($"Failed to send email to {email_address}");
    }

    [KernelFunction, Description("Lookup an email address for a person given a name")]
    public Task<string> GetEmailAddressAsync(
        ILogger logger,
        [Description("The name of the person to email.")] string? input = null)
    {
        throw new ArgumentException($"Email address for {input} not found!");
    }

    [KernelFunction, Description("Write a short poem for an e-mail")]
    public Task<string> WritePoemAsync(
        [Description("The topic of the poem.")] string input)
    {
        return Task.FromResult($"Roses are red, violets are blue, {input} is hard, so is this test.");
    }
}
