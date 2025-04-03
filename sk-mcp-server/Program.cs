using sk_mcp_server;
using sk_mcp_server.Tools;
using Microsoft.SemanticKernel;
using ModelContextProtocol.AspNetCore;


// Create a kernel builder and add plugins
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromType<DateTimeUtils>();
kernelBuilder.Plugins.AddFromType<WeatherUtils>();

// Build the kernel
Kernel kernel = kernelBuilder.Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//Add Mcp Server
builder.Services.AddMcpServer()
    .WithTools(kernel.Plugins);

var app = builder.Build();

app.MapMcp();

app.Run();
