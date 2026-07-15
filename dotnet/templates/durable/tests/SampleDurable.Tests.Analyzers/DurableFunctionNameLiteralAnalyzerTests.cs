using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using SampleDurable.Analyzers;

namespace SampleDurable.Tests.Analyzers;

[TestFixture]
public class DurableFunctionNameLiteralAnalyzerTests
{
    private static CSharpAnalyzerTest<DurableFunctionNameLiteralAnalyzer, DefaultVerifier> Build(string source) =>
        new()
        {
            TestState = { Sources = { DurableTestSources.Stubs, source } },
        };

    [Test]
    public async Task Should_report_string_literal_activity_name()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>({|#0:""DoWork""|});
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("CallActivityAsync"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_string_literal_sub_orchestrator_name()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallSubOrchestratorAsync<string>({|#0:""Sub""|});
    }
}";

        var test = Build(source);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("CI0018", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("CallSubOrchestratorAsync"));

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_nameof_activity_name()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class DoWorkActivity { }

public class MyOrchestrator
{
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(nameof(DoWorkActivity));
    }
}";

        await Build(source).RunAsync();
    }

    [Test]
    public async Task Should_not_report_const_activity_name()
    {
        var source = @"
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public class MyOrchestrator
{
    private const string ActivityName = ""DoWork"";

    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return await context.CallActivityAsync<string>(ActivityName);
    }
}";

        await Build(source).RunAsync();
    }
}
