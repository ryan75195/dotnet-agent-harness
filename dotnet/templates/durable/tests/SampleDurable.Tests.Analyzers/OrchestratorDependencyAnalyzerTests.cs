using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using SampleDurable.Analyzers;

namespace SampleDurable.Tests.Analyzers;

[TestFixture]
public class OrchestratorDependencyAnalyzerTests
{
    private static CSharpAnalyzerTest<OrchestratorDependencyAnalyzer, DefaultVerifier> Build(string source) =>
        new()
        {
            TestState = { Sources = { DurableTestSources.Stubs, source } },
        };

    [Test]
    public async Task Should_report_orchestrator_class_with_a_constructor_dependency()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public class {|#0:MyOrchestrator|}
{
    private IAgentClient _client;

    public MyOrchestrator(IAgentClient client) { _client = client; }

    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyOrchestrator"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_orchestrator_class_with_an_instance_field()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public class {|#0:MyOrchestrator|}
{
    private IAgentClient _client;

    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyOrchestrator"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_static_orchestrator_class()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class MyOrchestrator
{
    public const string EventName = ""Done"";

    public static async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_activity_class_with_a_dependency()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

public interface IAgentClient { Task<string> DispatchAsync(); }

public class MyActivity
{
    private IAgentClient _client;

    public MyActivity(IAgentClient client) { _client = client; }

    public Task<string> RunAsync([ActivityTrigger] string input) => _client.DispatchAsync();
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_class_with_a_dependency_and_a_context_taking_helper_method()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.DurableTask;

public interface IAgentClient { }

public class {|#0:MyOrchestratorHelper|}
{
    private IAgentClient _client;

    public MyOrchestratorHelper(IAgentClient client) { _client = client; }

    public Task<string> RunAsync(TaskOrchestrationContext context)
    {
        return context.CallActivityAsync<string>(""DoWork"");
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0017", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyOrchestratorHelper"));

        await test.RunAsync();
    }
}
