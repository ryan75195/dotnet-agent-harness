using McpServer.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices();
builder.Services.Add\u004dcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

var app = builder.Build();
app.MapMcp();
app.Run();

public partial class Program;
