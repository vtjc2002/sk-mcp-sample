﻿// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace sk_mcp_client;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        // Create an MCP client
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // Retrieve and display the list provided by the MCP server
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // Create a kernel and register the MCP tools
        Kernel kernel = CreateKernelWithChatCompletionService();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        // Enable automatic function calling
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        string prompt = "what's the weather like in Boston today?";
        Console.WriteLine(prompt);

        // Execute a prompt using the MCP tools. The AI model will automatically call the appropriate MCP tools to answer the prompt.
        FunctionResult result = await kernel.InvokePromptAsync(prompt, new(executionSettings));

        Console.WriteLine(result);

        // The expected output is: The likely color of the sky in Boston today is gray, as it is currently rainy.
    }

    /// <summary>
    /// Creates an instance of <see cref="Kernel"/> with the OpenAI chat completion service registered.
    /// </summary>
    /// <returns>An instance of <see cref="Kernel"/>.</returns>
    private static Kernel CreateKernelWithChatCompletionService()
    {
        // Load and validate configuration
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        if (config["AzureOpenAI:Endpoint"] is not { } endPoint)
        {
            const string Message = "Please provide a valid AzureOpenAI:Endpoint to run this sample.";
            Console.Error.WriteLine(Message);
            throw new InvalidOperationException(Message);
        }

        if (config["AzureOpenAI:ApiKey"] is not { } apiKey)
        {
            const string Message = "Please provide a valid AzureOpenAI:ApiKey to run this sample.";
            Console.Error.WriteLine(Message);
            throw new InvalidOperationException(Message);
        }

        string modelId = config["AzureOpenAI:ChatModelId"] ?? "gpt-4o-mini";

        // Create kernel
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddAzureOpenAIChatCompletion(endpoint: endPoint, deploymentName: modelId, apiKey: apiKey);

        return kernelBuilder.Build();
    }

    /// <summary>
    /// Creates an MCP client and connects it to the MCPServer server.
    /// </summary>
    /// <returns>An instance of <see cref="IMcpClient"/>.</returns>
    private static Task<IMcpClient> CreateMcpClientAsync()
    {
        SseClientTransportOptions transportOptions = new()
        {
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            MaxReconnectAttempts = 3,
            ReconnectDelay = TimeSpan.FromSeconds(2)
        };

        return McpClientFactory.CreateAsync(
            new McpServerConfig()
            {
                Id = "sk-mcp-server",
                Name = "Sk MCPServer",
                TransportType = TransportTypes.Sse,
                Location = "http://localhost:5057/sse",
                TransportOptions = new () { 
                    ["connectionTimeout"] = transportOptions.ConnectionTimeout.Seconds.ToString(),
                    ["maxReconnectAttempts"] = transportOptions.MaxReconnectAttempts.ToString(),
                    ["reconnectDelay"] = transportOptions.ReconnectDelay.Seconds.ToString()
                    }

            },
            new McpClientOptions()
            {
                ClientInfo = new() { Name = "MCPClient", Version = "1.0.0" }
            }
        );
    }

    /// <summary>
    /// Displays the list of available MCP tools.
    /// </summary>
    /// <param name="tools">The list of the tools to display.</param>
    private static void DisplayTools(IList<McpClientTool> tools)
    {
        Console.WriteLine("Available MCP tools:");
        foreach (var tool in tools)
        {
            Console.WriteLine($"- {tool.Name}: {tool.Description}");
        }
    }
}