using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using SampleDurable.Analyzers;

namespace SampleDurable.Tests.Analyzers;

[TestFixture]
public class OrchestratorNonDurableAwaitAnalyzerTests
{
    private static CSharpAnalyzerTest<OrchestratorNonDurableAwaitAnalyzer, DefaultVerifier> Build(string source) =>
        new()
        {
            TestState = { Sources = { DurableTestSources.Stubs, source } },
        };

    [Test]
    public async Task Should_report_await_on_an_injected_service()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyOrchestrator
{
    private IAgentClient _client;

    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await {|#0:_client.DispatchAsync()|};
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_client.DispatchAsync()"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_await_on_a_static_io_call()
    {
        var source = @"
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        using var http = new HttpClient();
        return await {|#0:http.GetStringAsync(""https://example.com"")|};
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"http.GetStringAsync(""https://example.com"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_await_on_call_activity()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_task_when_all_over_context_calls()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string[]> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var tasks = new List<Task<string>>();
        for (var i = 0; i < 3; i++)
        {
            tasks.Add(context.CallSubOrchestratorAsync<string>(""Sub"", i));
        }

        return await Task.WhenAll(tasks);
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_task_when_any_racing_event_against_timer()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var completed = context.WaitForExternalEvent<string>(""Done"");
        var timeout = context.CreateTimer(context.CurrentUtcDateTime.AddHours(1), CancellationToken.None);

        var winner = await Task.WhenAny(completed, timeout);
        if (winner == completed)
        {
            return await completed;
        }

        return ""timed out"";
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_await_on_an_entity_signal()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

public class MyOrchestrator
{
    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var counter = new EntityInstanceId(""RunCounterEntity"", context.InstanceId);
        await context.Entities.SignalEntityAsync(counter, ""Dispatched"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_await_outside_an_orchestrator()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyActivity
{
    private IAgentClient _client;

    public async Task<string> RunAsync([ActivityTrigger] string input)
    {
        return await _client.DispatchAsync();
    }
}";

        await Build(source).RunAsync();
    }
}
