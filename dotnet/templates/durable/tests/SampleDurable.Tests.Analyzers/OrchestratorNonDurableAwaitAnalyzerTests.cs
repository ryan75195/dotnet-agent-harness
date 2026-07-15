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
    public async Task Should_not_report_await_on_a_foreach_task_local()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<List<string>> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var tasks = new List<Task<string>>();
        for (var i = 0; i < 3; i++)
        {
            tasks.Add(context.CallSubOrchestratorAsync<string>(""Sub"", i));
        }

        var results = new List<string>();
        foreach (var t in tasks)
        {
            results.Add(await t);
        }

        return results;
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_configure_await_on_a_durable_call()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"").ConfigureAwait(false);
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_configure_await_on_a_non_durable_call()
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
        return await {|#0:_client.DispatchAsync().ConfigureAwait(false)|};
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_client.DispatchAsync().ConfigureAwait(false)"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_await_foreach_over_a_non_durable_stream()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await foreach (var chunk in {|#0:_client.StreamAsync(""go"")|})
        {
            await context.CallActivityAsync<string>(""Handle"", chunk);
        }
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"_client.StreamAsync(""go"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_await_using_a_non_durable_client()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await using var session = {|#0:_client.OpenSession(""go"")|};
        await context.CallActivityAsync<string>(""Handle"");
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"_client.OpenSession(""go"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_inline_non_durable_call_inside_task_when_all()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyOrchestrator
{
    private IAgentClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var durable = context.CallActivityAsync<string>(""A"");
        await {|#0:Task.WhenAll(_client.DispatchAsync(), durable)|};
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Task.WhenAll(_client.DispatchAsync(), durable)"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_inline_durable_calls_inside_task_when_all()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await Task.WhenAll(
            context.CallActivityAsync<string>(""A""),
            context.CallActivityAsync<string>(""B""));
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_non_durable_await_in_a_method_taking_an_orchestration_context()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.DurableTask;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyOrchestrator : TaskOrchestrator<string, string>
{
    private IAgentClient _client;

    public override async Task<string> RunAsync(TaskOrchestrationContext context, string input)
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
    public async Task Should_not_report_await_on_task_completed_task()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await Task.CompletedTask;
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_await_using_a_block_over_a_non_durable_client()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await using (var session = {|#0:_client.OpenSession(""go"")|})
        {
            await context.CallActivityAsync<string>(""Handle"");
        }
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"_client.OpenSession(""go"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_await_using_a_block_over_a_non_durable_expression()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await using ({|#0:_client.OpenSession(""go"")|})
        {
            await context.CallActivityAsync<string>(""Handle"");
        }
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"_client.OpenSession(""go"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_await_foreach_deconstructing_a_non_durable_stream()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await foreach (var (key, value) in {|#0:_client.StreamPairsAsync()|})
        {
            await context.CallActivityAsync<string>(""Handle"", key);
        }
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_client.StreamPairsAsync()"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_once_when_an_await_using_initializer_is_awaited()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Testing;

public class MyOrchestrator
{
    private IAgentStreamClient _client;

    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await using var session = await {|#0:_client.OpenSessionAsync(""go"")|};
        await context.CallActivityAsync<string>(""Handle"");
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(@"_client.OpenSessionAsync(""go"")"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_await_on_a_helper_taking_an_orchestration_context()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await RunAgent(context, ""a"");
    }

    private static Task<string> RunAgent(TaskOrchestrationContext context, string name)
        => context.CallSubOrchestratorAsync<string>(""Agent"", name);
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_task_when_all_over_helpers_taking_an_orchestration_context()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await Task.WhenAll(RunAgent(context, ""a""), RunAgent(context, ""b""));
    }

    private static Task<string> RunAgent(TaskOrchestrationContext context, string name)
        => context.CallSubOrchestratorAsync<string>(""Agent"", name);
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_a_non_durable_await_inside_a_context_taking_helper()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyOrchestrator
{
    private static IAgentClient _client;

    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await RunAgent(context, ""a"");
    }

    private static async Task<string> RunAgent(TaskOrchestrationContext context, string name)
        => await {|#0:_client.DispatchAsync()|};
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0016", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_client.DispatchAsync()"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_await_on_a_task_list_element()
    {
        var source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var tasks = new List<Task<string>>();
        for (var i = 0; i < 3; i++)
        {
            tasks.Add(context.CallSubOrchestratorAsync<string>(""Sub"", i));
        }

        return await tasks[0];
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
