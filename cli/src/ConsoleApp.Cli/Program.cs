using ConsoleApp.Core;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCoreServices();
await builder.Build().RunAsync();
