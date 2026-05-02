using ConsoleApp.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace ConsoleApp.Tests.Analyzers;

[TestFixture]
public class ConstructorDependencyAnalyzerTests
{
    [Test]
    public async Task Should_report_when_constructor_creates_disposable()
    {
        var source = """
            using System;

            public class ExternalClient : IDisposable
            {
                public void Dispose() { }
            }

            public class MyService
            {
                private readonly ExternalClient _client;

                public MyService()
                {
                    _client = new ExternalClient();
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0003")
            .WithSpan(14, 19, 14, 39)
            .WithArguments("MyService", "ExternalClient");

        var test = new CSharpAnalyzerTest<ConstructorDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_non_disposable_types()
    {
        var source = """
            using System.Collections.Generic;

            public class MyService
            {
                private readonly List<string> _items;

                public MyService()
                {
                    _items = new List<string>();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_disposable_created_inside_lambda()
    {
        var source = """
            using System;

            public class Worker : IDisposable
            {
                public void Dispose() { }
            }

            public class MyPool
            {
                private readonly Func<Worker> _factory;

                public MyPool()
                {
                    _factory = () => new Worker();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_not_report_for_threading_primitives()
    {
        var source = """
            using System.Threading;

            public class MyService
            {
                private readonly SemaphoreSlim _semaphore;

                public MyService()
                {
                    _semaphore = new SemaphoreSlim(10);
                }
            }
            """;

        var test = new CSharpAnalyzerTest<ConstructorDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task Should_report_for_async_disposable()
    {
        var source = """
            using System;
            using System.Threading.Tasks;

            public class AsyncClient : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;
            }

            public class MyService
            {
                private readonly AsyncClient _client;

                public MyService()
                {
                    _client = new AsyncClient();
                }
            }
            """;

        var expected = DiagnosticResult.CompilerWarning("CI0003")
            .WithSpan(15, 19, 15, 36)
            .WithArguments("MyService", "AsyncClient");

        var test = new CSharpAnalyzerTest<ConstructorDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ExpectedDiagnostics = { expected },
        };

        await test.RunAsync();
    }
}
