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

    [Test]
    public async Task Should_report_orchestrator_class_with_inherited_protected_field()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public abstract class OrchestratorBase
{
    protected IAgentClient _client;
}

public class {|#0:MyOrchestrator|} : OrchestratorBase
{
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
    public async Task Should_not_report_orchestrator_class_with_private_base_field()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public abstract class OrchestratorBase
{
    private IAgentClient _client;
}

public class MyOrchestrator : OrchestratorBase
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_record_orchestrator_with_primary_constructor()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public record {|#0:MyOrchestrator|}(IAgentClient Client)
{
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
    public async Task Should_not_report_stateless_record_orchestrator()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public record MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_static_orchestrator_class_with_a_mutable_static_field()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class {|#0:MyOrchestrator|}
{
    private static int _attempts;

    public static async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        _attempts++;
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
    public async Task Should_not_report_static_orchestrator_class_with_const_and_static_readonly()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class MyOrchestrator
{
    public const string EventName = ""Done"";

    public static readonly TimeSpan Timeout = TimeSpan.FromHours(2);

    public static async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(""DoWork"");
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_static_orchestrator_class_with_a_settable_static_property()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class {|#0:MyOrchestrator|}
{
    private static int Attempts { get; set; }

    public static async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        Attempts++;
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
    public async Task Should_not_report_static_orchestrator_class_with_a_get_only_static_property()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class MyOrchestrator
{
    private static string ActivityName => ""DoWork"";

    public static async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(ActivityName);
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_report_orchestrator_class_with_constructor_parameter_and_no_field()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public interface IAgentClient { }

public static class AgentClientRegistry
{
    public static void Register(IAgentClient client) { }
}

public class {|#0:MyOrchestrator|}
{
    public MyOrchestrator(IAgentClient client)
    {
        AgentClientRegistry.Register(client);
    }

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
}
