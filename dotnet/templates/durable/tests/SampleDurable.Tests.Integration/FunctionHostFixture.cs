using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using SampleDurable.Functions.Triggers;

namespace SampleDurable.Tests.Integration;

[SetUpFixture]
public class FunctionHostFixture
{
    private const int HostPort = 7099;
    private const int AzuriteTablePort = 10002;
    private const int TailBufferSize = 80;
    private const string AdminFunctionsPath = "../admin/functions";
    private const string IndexedMarker = "indexed";

    private static Process? _host;
    private static readonly ConcurrentQueue<string> OutputTail = new();

    public static Uri BaseAddress { get; } = new($"http://localhost:{HostPort}/api/");

    [OneTimeSetUp]
    public async Task StartHost()
    {
        if (!await PortIsOpen(AzuriteTablePort))
        {
            Assert.Fail(
                $"Azurite is not listening on {AzuriteTablePort}. Durable Functions needs blob (10000), " +
                "queue (10001) and table (10002). Start it with 'azurite --silent --inMemoryPersistence " +
                "--skipApiVersionCheck' and re-run.");
        }

        var projectDir = ResolveFunctionsProjectDirectory();

        _host = StartCoreTools(projectDir);

        if (_host is null)
        {
            return;
        }

        _host.OutputDataReceived += (_, args) => CaptureLine(args.Data);
        _host.ErrorDataReceived += (_, args) => CaptureLine(args.Data);
        _host.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        await WaitForHost();
    }

    private static Process? StartCoreTools(string projectDir)
    {
        try
        {
            return Process.Start(new ProcessStartInfo("func", $"start --port {HostPort}")
            {
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        }
        catch (Win32Exception exception)
        {
            Assert.Fail(
                "Could not start Azure Functions Core Tools ('func'). Install v4 and ensure it is on PATH. " +
                $"Underlying error: {exception.Message}");
            return null;
        }
    }

    private static void CaptureLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        OutputTail.Enqueue(line);
        while (OutputTail.Count > TailBufferSize && OutputTail.TryDequeue(out _))
        {
        }
    }

    [OneTimeTearDown]
    public void StopHost()
    {
        if (_host is { HasExited: false })
        {
            _host.Kill(entireProcessTree: true);
        }

        _host?.Dispose();
    }

    private static string ResolveFunctionsProjectDirectory()
    {
        var solutionDirectory = FindSolutionDirectory(AppContext.BaseDirectory);
        if (solutionDirectory is null)
        {
            Assert.Fail(
                $"Could not locate SampleDurable.slnx above {AppContext.BaseDirectory}. " +
                "The durable template's directory layout may have changed.");
        }

        return Path.Combine(solutionDirectory!.FullName, "src", "SampleDurable.Functions");
    }

    private static DirectoryInfo? FindSolutionDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.GetFiles("*.slnx").Length > 0)
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static async Task WaitForHost()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        var deadline = DateTime.UtcNow.AddMinutes(2);
        var lastSeen = "(never responded)";

        while (DateTime.UtcNow < deadline)
        {
            lastSeen = await ProbeIndexedFunctions(client);
            if (lastSeen is IndexedMarker)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var tail = string.Join(Environment.NewLine, OutputTail);
        Assert.Fail(
            $"Functions host did not index {nameof(RunWebhookTrigger)} on port {HostPort} within 2 minutes. " +
            $"Last probe of {AdminFunctionsPath}: {lastSeen}.{Environment.NewLine}Last output:{Environment.NewLine}{tail}");
    }

    private static async Task<string> ProbeIndexedFunctions(HttpClient client)
    {
        try
        {
            var response = await client.GetAsync(new Uri(BaseAddress, AdminFunctionsPath));
            if (!response.IsSuccessStatusCode)
            {
                return $"HTTP {(int)response.StatusCode}";
            }

            var body = await response.Content.ReadAsStringAsync();
            return body.Contains(nameof(RunWebhookTrigger), StringComparison.Ordinal)
                ? IndexedMarker
                : "responded, but no functions indexed yet";
        }
        catch (HttpRequestException exception)
        {
            return exception.Message;
        }
        catch (TaskCanceledException)
        {
            return "request timed out";
        }
    }

    private static async Task<bool> PortIsOpen(int port)
    {
        try
        {
            using var probe = new TcpClient();
            await probe.ConnectAsync("localhost", port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
