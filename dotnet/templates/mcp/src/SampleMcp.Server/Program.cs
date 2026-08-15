using SampleMcp.Core;
using SampleMcp.Server.Authentication;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddMcpOAuth(builder.Configuration);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

var app = builder.Build();
app.UseForwardedHeaders();
var authEnabled = app.Configuration.IsMcpOAuthEnabled();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

var mcp = app.MapMcp();
if (authEnabled)
{
    mcp.RequireAuthorization();
}

app.Run();

public partial class Program;
