using Microsoft.Extensions.Hosting;
using SampleDurable.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => services.AddCoreServices())
    .Build();

host.Run();

public partial class Program;
