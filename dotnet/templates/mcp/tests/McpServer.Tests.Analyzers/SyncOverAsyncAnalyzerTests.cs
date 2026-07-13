using McpServer.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace McpServer.Tests.Analyzers;

[TestFixture]
public class SyncOverAsyncAnalyzerTests
{
    [Test]
    public async Task Should_report_task_result_access()
    {
        var source = @"
using System.Threading.Tasks;

public class MyService
{
    public void DoWork()
    {
        var task = Task.FromResult(42);
        var value = task.{|#0:Result|};
    }
}";

        var expected = new DiagnosticResult("CI0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("task", "Result");

        var test = new CSharpAnalyzerTest<SyncOverAsyncAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_task_wait()
    {
        var source = @"
using System.Threading.Tasks;

public class MyService
{
    public void DoWork()
    {
        var task = Task.CompletedTask;
        task.{|#0:Wait|}();
    }
}";

        var expected = new DiagnosticResult("CI0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("task", "Wait()");

        var test = new CSharpAnalyzerTest<SyncOverAsyncAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_get_awaiter_get_result()
    {
        var source = @"
using System.Threading.Tasks;

public class MyService
{
    public void DoWork()
    {
        var task = Task.FromResult(42);
        var value = task.GetAwaiter().{|#0:GetResult|}();
    }
}";

        var expected = new DiagnosticResult("CI0015", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("task", "GetAwaiter().GetResult()");

        var test = new CSharpAnalyzerTest<SyncOverAsyncAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_await()
    {
        var source = @"
using System.Threading.Tasks;

public class MyService
{
    public async Task DoWork()
    {
        var value = await Task.FromResult(42);
    }
}";

        var test = new CSharpAnalyzerTest<SyncOverAsyncAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { source } },
        };

        await test.RunAsync();
    }
}
