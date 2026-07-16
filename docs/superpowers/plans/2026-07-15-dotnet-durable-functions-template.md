# .NET Azure Durable Functions Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `dotnet new durable` template — a .NET 10 isolated-worker Azure Durable Functions solution with orchestrator-determinism guardrails at error severity — and wire it into the `new-project` CLI, plugin, and CI as a first-class template type.

**Architecture:** Copy the `mcp` template's guardrail skeleton (analyzers CI0001–CI0015, architecture tests, hooks, agent configs) into `dotnet/templates/durable`, replacing the MCP host with a Functions isolated host. Add three new Roslyn analyzers (CI0016–CI0018) that close the gaps Microsoft's bundled `DURABLE*` rules leave, plus an architecture rule confining orchestrations to `Core.Models`. The sample orchestration demonstrates webhook-start → fan-out → dispatch → await-external-event-or-timeout → aggregate, plus a durable entity.

**Tech Stack:** .NET 10, Azure Functions isolated worker v4, Durable Functions (Azure Storage backend), Roslyn analyzers (netstandard2.0), NUnit + NSubstitute + FluentAssertions, NetArchTest, Azurite, PowerShell.

**Spec:** `docs/superpowers/specs/2026-07-15-dotnet-durable-functions-template-design.md`

## Global Constraints

These apply to **every** task. They are not repeated per-task.

- **Template source name is `SampleDurable`.** `dotnet new` substitutes it for the user's `-n` value. Every namespace, project name, and file path uses it verbatim. Never write `MyProject`, `Foo`, or a placeholder name into template files.
- **No comments in any C# file.** `NoCommentsAnalyzer` (CI0013) blocks `//`, `/* */`, and `///` at **error** severity. This includes the analyzer projects' own sources. Extract intent into names.
- **`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`.** Any CA/IDE/CS/CI/DURABLE diagnostic at error severity breaks the build. `NU1510` is exempt (`WarningsNotAsErrors`).
- **File-scoped namespaces, Allman braces, `_camelCase` private fields, `I`-prefixed interfaces, nullable enabled.** Enforced by `.editorconfig` + `dotnet format`.
- **No `Async` suffix on any method we declare.** `NamingConventionTests.Should_not_use_async_suffix_on_method_names` scans the `Core` **and** `Functions` assemblies for public **instance** methods ending in `Async` and fails the build for any not in its framework allowlist (`DisposeAsync`, `ExecuteAsync`, `StartAsync`, `StopAsync`). Consequences, discovered during Task 2 and binding on everything after it:
  - Core interfaces are `IAgentDispatcher.Dispatch` and `IResultPublisher.Publish` — **not** `DispatchAsync`/`PublishAsync`.
  - **Every function entry point is named `Run`, never `RunAsync`.** This is also the Azure Functions idiom (their own templates use `Run`); the method name is arbitrary because `[Function(name)]` carries the function's real name. Static orchestrators/triggers would technically slip past the rule (it only scans instance methods), but they use `Run` too — uniformity beats exploiting a loophole.
  - Calling an SDK method that ends in `Async` is fine — the rule only sees methods *we declare*. `context.CallActivityAsync(...)`, `dispatcher.DispatchAsync(this)` on the SDK's `TaskEntityDispatcher`, and the Roslyn test harness's `test.RunAsync()` all stay exactly as written.
- **Concrete public classes need a recognised role suffix.** `NamingConventionTests.AllowedSuffixes` is a fixed vocabulary; a class whose name ends in none of them fails. Task 2 added `Dispatcher` and `Publisher`; Task 3 adds `Activity`, `Orchestrator`, and `Trigger`. `Entity` already exists. Static classes compile to `abstract sealed` and are skipped by the rule, but the vocabulary must still cover them so a forker writing a non-static one isn't blocked. Extend the list in the test — never rename a domain class to satisfy it.
- **Analyzer projects target `netstandard2.0`**; everything else targets `net10.0`. `Directory.Build.props` excludes `SampleDurable.Analyzers` from `TreatWarningsAsErrors` and from the analyzer ProjectReference (an analyzer cannot analyze itself).
- **Package version floors:** `Microsoft.Azure.Functions.Worker` ≥ 2.50.0 (use 2.52.0), `Microsoft.Azure.Functions.Worker.Sdk` 2.0.7, `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` 1.18.0. **Re-verify each against the restored package before pinning** — the numbers above were current on 2026-07-15 and are starting pins, not gospel. If a version does not resolve, use the latest stable that does and note it in the commit message.
- **Do NOT add `Microsoft.DurableTask.Analyzers`.** The `DURABLE*` analyzers are already bundled in `Worker.Extensions.DurableTask` v1.6.0+ and enabled by default. That separate package is for the standalone Durable Task SDK and will conflict.
- **Follow `mcp`, not `etl-api`, where templates diverge.** No `.codex/` directory. Two `.claude/hooks` (`block-main-branch.sh`, `block-merged-branch.sh`), not three.
- **Isolated worker only.** The in-process model retires 2026-11-10 and never supported .NET 10.
- **Copy verbatim, rename mechanically.** Files copied from `mcp` change only `SampleMcp` → `SampleDurable`. Do not "improve" them in passing — divergence between templates is a cost.

---

### Task 1: Template skeleton that scaffolds and builds

Produces an installable `dotnet new durable` template with the full guardrail skeleton and a `Core` project, but no Functions host yet. Deliverable: `dotnet new durable -n Foo` builds and tests green.

**Files:**
- Create: `dotnet/templates/durable/` (whole tree, copied from `dotnet/templates/mcp`)
- Create: `dotnet/templates/durable/.template.config/template.json`
- Create: `dotnet/templates/durable/SampleDurable.slnx`
- Create: `dotnet/templates/durable/harness-manifest.json`
- Modify: `dotnet/templates/durable/setup.ps1` (commit message only)
- Delete: everything under `src/SampleDurable.Server/`, `tests/SampleDurable.Tests.*/` that is MCP-specific

**Interfaces:**
- Consumes: nothing.
- Produces: the template tree at `dotnet/templates/durable/`; projects `SampleDurable.Core`, `SampleDurable.Analyzers`, `SampleDurable.Tests.{Unit,Integration,Architecture,Analyzers}`; namespace root `SampleDurable`.

- [ ] **Step 1: Copy the mcp template and rename mechanically**

```powershell
cd C:\Users\ryan7\programming\agent-project-templates
Copy-Item -Recurse dotnet\templates\mcp dotnet\templates\durable

Get-ChildItem dotnet\templates\durable -Recurse -Directory |
  Where-Object { $_.Name -match 'bin|obj' } |
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# ENCODING HAZARD - read this before changing the loop below.
# On Windows PowerShell 5.1:
#   * `Get-Content -Raw` with no `-Encoding` decodes UTF-8 bytes as ANSI (CP1252),
#     so every em-dash becomes 'a-EUR-"'. Writing that back re-encodes the mojibake
#     permanently. `-Encoding utf8` on Get-Content is the minimum fix.
#   * `Set-Content -Encoding utf8` ALWAYS emits a BOM on 5.1 (utf8NoBOM only exists
#     on PowerShell Core 6+), so it silently adds a BOM to files that had none.
# Both corruptions are invisible to dotnet build, dotnet format, and the test suite.
# The .NET APIs below round-trip UTF-8 correctly and preserve each file's existing
# BOM state, so they are safe on both 5.1 and Core.

Get-ChildItem dotnet\templates\durable -Recurse -File | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    if ($text -match 'SampleMcp') {
        $text = $text -replace 'SampleMcp', 'SampleDurable'
        [System.IO.File]::WriteAllText($_.FullName, $text, (New-Object System.Text.UTF8Encoding($hasBom)))
    }
}

Get-ChildItem dotnet\templates\durable -Recurse |
  Where-Object { $_.Name -match 'SampleMcp' } |
  Sort-Object { $_.FullName.Length } -Descending |
  ForEach-Object { Rename-Item $_.FullName ($_.Name -replace 'SampleMcp','SampleDurable') }
```

- [ ] **Step 2: Verify no `SampleMcp` references survive**

Run:
```powershell
Get-ChildItem dotnet\templates\durable -Recurse |
  Select-String -Pattern 'SampleMcp' -SimpleMatch
```
Expected: no output. Directory names are renamed longest-first so nested paths rename before their parents.

- [ ] **Step 3: Delete the MCP-specific surface**

```powershell
cd dotnet\templates\durable
Remove-Item -Recurse -Force src\SampleDurable.Server
Remove-Item -Recurse -Force tests\SampleDurable.Tests.Unit\Server
Remove-Item -Force tests\SampleDurable.Tests.Integration\ProtocolTests.cs
Remove-Item -Recurse -Force src\SampleDurable.Core\Interfaces, src\SampleDurable.Core\Services
Remove-Item -Force tests\SampleDurable.Tests.Unit\Core\GreetingServiceTests.cs
```

Deleting `Interfaces/` and `Services/` orphans `ServiceCollectionExtensions.cs`, which still references `IGreetingService`. Reduce it to an empty registration so the solution compiles; Task 2 fills it in:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace SampleDurable.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        return services;
    }
}
```

`DiRegistrationTests` asserts every public `Core.Interfaces` type is registered here. With no interfaces yet, it passes vacuously — correct at this stage, and Task 2 gives it something to check.

- [ ] **Step 4: Write `.template.config/template.json`**

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "ryan75195",
  "classifications": ["Durable", "Functions", "Serverless", "Workflow"],
  "identity": "SampleDurable.Template",
  "name": "Azure Durable Functions solution",
  "shortName": "durable",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "SampleDurable",
  "preferNameDirectory": true,
  "primaryOutputs": [
    { "path": "src/SampleDurable.Functions/SampleDurable.Functions.csproj" }
  ],
  "postActions": [
    {
      "id": "restore",
      "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
      "continueOnError": true,
      "manualInstructions": [{ "text": "Run 'dotnet restore'" }]
    }
  ]
}
```

- [ ] **Step 5: Write `SampleDurable.slnx`**

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/SampleDurable.Analyzers/SampleDurable.Analyzers.csproj" />
    <Project Path="src/SampleDurable.Functions/SampleDurable.Functions.csproj" />
    <Project Path="src/SampleDurable.Core/SampleDurable.Core.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/SampleDurable.Tests.Analyzers/SampleDurable.Tests.Analyzers.csproj" />
    <Project Path="tests/SampleDurable.Tests.Architecture/SampleDurable.Tests.Architecture.csproj" />
    <Project Path="tests/SampleDurable.Tests.Integration/SampleDurable.Tests.Integration.csproj" />
    <Project Path="tests/SampleDurable.Tests.Unit/SampleDurable.Tests.Unit.csproj" />
  </Folder>
</Solution>
```

The `Functions` project does not exist until Task 3. Comment it out of the `.slnx` **only if** it blocks this task's build; re-add in Task 3. Prefer creating an empty placeholder csproj in Step 7 instead.

- [ ] **Step 6: Update `harness-manifest.json`**

```json
{
  "ownedPaths": [
    ".githooks/**",
    ".claude/**",
    ".editorconfig",
    "tests/.editorconfig",
    "Directory.Build.props",
    "host.json",
    "src/SampleDurable.Analyzers/**",
    "CLAUDE.md",
    "harness-manifest.json"
  ]
}
```

- [ ] **Step 7: Create a minimal placeholder `Functions` project so the solution resolves**

Create `src/SampleDurable.Functions/SampleDurable.Functions.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\SampleDurable.Core\SampleDurable.Core.csproj" />
  </ItemGroup>
</Project>
```

Create `src/SampleDurable.Functions/AssemblyMarker.cs`:

```csharp
namespace SampleDurable.Functions;

public sealed class AssemblyMarker;
```

- [ ] **Step 8: Fix the copied test helpers to reference `Functions` instead of `Server`**

In `tests/SampleDurable.Tests.Architecture/TestHelpers.cs`, replace the `ServerAssembly` property:

```csharp
public static Assembly FunctionsAssembly => typeof(Functions.AssemblyMarker).Assembly;
```

In `tests/SampleDurable.Tests.Architecture/LayerDependencyTests.cs`, replace `Should_keep_server_depending_only_on_first_party_core` with:

```csharp
    [Test]
    public void Should_keep_functions_depending_only_on_first_party_core()
    {
        var functionsAssembly = Assembly.Load("SampleDurable.Functions");
        var firstPartyRefs = functionsAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name?.StartsWith("SampleDurable", StringComparison.Ordinal) == true)
            .ToList();

        firstPartyRefs.Should().BeEquivalentTo(new[] { "SampleDurable.Core" },
            "Functions is the sole entry point and may only reference Core within first-party assemblies");
    }
```

The copied `LayerDependencyTests` reference `SampleDurable.Core.Data` and `Microsoft.EntityFrameworkCore`. Neither exists here — the assertions pass vacuously. **Leave them.** `mcp` carries the same vacuous rules; removing them widens template divergence for no benefit.

Update the `Tests.Architecture` and `Tests.Unit`/`Tests.Integration` csproj `ProjectReference` entries from `SampleDurable.Server` to `SampleDurable.Functions`.

- [ ] **Step 9: Update setup.ps1's commit message**

In `setup.ps1`, change:

```powershell
Invoke-Git commit -q --no-verify -m "Initial scaffold from durable template"
```

- [ ] **Step 10: Scaffold and verify the skeleton builds green**

Run:
```powershell
cd C:\Users\ryan7\programming\agent-project-templates
dotnet new install .\dotnet --force
dotnet new durable -n ScaffoldSmoke -o $env:TEMP\scaffold-durable
cd $env:TEMP\scaffold-durable
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test --verbosity minimal
```
Expected: build succeeds, format clean, all tests pass. `Tests.Unit` will have only `SmokeTests`; that is correct at this stage.

Clean up: `cd C:\Users\ryan7\programming\agent-project-templates; Remove-Item -Recurse -Force $env:TEMP\scaffold-durable`

- [ ] **Step 11: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable template skeleton copied from mcp"
```

---

### Task 2: Core domain — models, interfaces, stub services

The external system stays stubbed behind a Core interface; nothing in this template knows what an agent or a GitHub issue is.

**Files:**
- Create: `src/SampleDurable.Core/Models/AgentWorkItem.cs`, `AgentRunRequest.cs`, `AgentDispatch.cs`, `AgentResult.cs`, `AgentRunSummary.cs`, `RunCounterState.cs`
- Create: `src/SampleDurable.Core/Interfaces/IAgentDispatcher.cs`, `IResultPublisher.cs`
- Create: `src/SampleDurable.Core/Services/StubAgentDispatcher.cs`, `StubResultPublisher.cs`
- Modify: `src/SampleDurable.Core/ServiceCollectionExtensions.cs`
- Test: `tests/SampleDurable.Tests.Unit/Core/StubAgentDispatcherTests.cs`

**Interfaces:**
- Consumes: Task 1's `SampleDurable.Core` project.
- Produces:
  - `record AgentWorkItem(string Id, string Prompt)`
  - `record AgentRunRequest(string RunKey, IReadOnlyList<AgentWorkItem> Items)`
  - `record AgentDispatch(string DispatchId, string WorkItemId)`
  - `record AgentResult(string WorkItemId, bool Succeeded, string Output)`
  - `record AgentRunSummary(int Total, int Succeeded, int TimedOut)`
  - `record RunCounterState(int Dispatched, int Completed, int TimedOut)`
  - `IAgentDispatcher.Dispatch(AgentWorkItem, CancellationToken) → Task<AgentDispatch>`
  - `IResultPublisher.Publish(AgentRunSummary, CancellationToken) → Task`
  - `ServiceCollectionExtensions.AddCoreServices(IServiceCollection) → IServiceCollection`

- [ ] **Step 1: Write the failing test**

Create `tests/SampleDurable.Tests.Unit/Core/StubAgentDispatcherTests.cs`:

```csharp
using FluentAssertions;
using SampleDurable.Core.Models;
using SampleDurable.Core.Services;

namespace SampleDurable.Tests.Unit.Core;

[TestFixture]
public class StubAgentDispatcherTests
{
    [Test]
    public async Task Should_return_dispatch_carrying_the_work_item_id()
    {
        var dispatcher = new StubAgentDispatcher();
        var item = new AgentWorkItem("item-7", "summarize the issue");

        var dispatch = await dispatcher.Dispatch(item, CancellationToken.None);

        using (new AssertionScope())
        {
            dispatch.WorkItemId.Should().Be("item-7");
            dispatch.DispatchId.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Test]
    public async Task Should_return_distinct_dispatch_ids_for_distinct_items()
    {
        var dispatcher = new StubAgentDispatcher();

        var first = await dispatcher.Dispatch(new AgentWorkItem("a", "p"), CancellationToken.None);
        var second = await dispatcher.Dispatch(new AgentWorkItem("b", "p"), CancellationToken.None);

        first.DispatchId.Should().NotBe(second.DispatchId);
    }
}
```

Add `using FluentAssertions.Execution;` for `AssertionScope`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/SampleDurable.Tests.Unit --filter StubAgentDispatcherTests`
Expected: FAIL — `StubAgentDispatcher` and the model types do not exist (CS0246).

- [ ] **Step 3: Write the models**

`src/SampleDurable.Core/Models/AgentWorkItem.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record AgentWorkItem(string Id, string Prompt);
```

`src/SampleDurable.Core/Models/AgentRunRequest.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record AgentRunRequest(string RunKey, IReadOnlyList<AgentWorkItem> Items);
```

`src/SampleDurable.Core/Models/AgentDispatch.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record AgentDispatch(string DispatchId, string WorkItemId);
```

`src/SampleDurable.Core/Models/AgentResult.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record AgentResult(string WorkItemId, bool Succeeded, string Output);
```

`src/SampleDurable.Core/Models/AgentRunSummary.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record AgentRunSummary(int Total, int Succeeded, int TimedOut);
```

`src/SampleDurable.Core/Models/RunCounterState.cs`:
```csharp
namespace SampleDurable.Core.Models;

public record RunCounterState(int Dispatched, int Completed, int TimedOut)
{
    public static RunCounterState Empty => new(0, 0, 0);
}
```

- [ ] **Step 4: Write the interfaces**

`src/SampleDurable.Core/Interfaces/IAgentDispatcher.cs`:
```csharp
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Interfaces;

public interface IAgentDispatcher
{
    Task<AgentDispatch> Dispatch(AgentWorkItem workItem, CancellationToken cancellationToken);
}
```

`src/SampleDurable.Core/Interfaces/IResultPublisher.cs`:
```csharp
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Interfaces;

public interface IResultPublisher
{
    Task Publish(AgentRunSummary summary, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Write the stub services**

`src/SampleDurable.Core/Services/StubAgentDispatcher.cs`:
```csharp
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Services;

public sealed class StubAgentDispatcher : IAgentDispatcher
{
    public Task<AgentDispatch> Dispatch(AgentWorkItem workItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        var dispatch = new AgentDispatch($"dispatch-{workItem.Id}-{Guid.NewGuid():N}", workItem.Id);
        return Task.FromResult(dispatch);
    }
}
```

`Guid.NewGuid()` is legal here — this is an **activity-side service**, not an orchestrator. `DURABLE0002` and CI0016 are scoped to `[OrchestrationTrigger]` methods only. This asymmetry is the point of the design and should not be "fixed".

`src/SampleDurable.Core/Services/StubResultPublisher.cs`:
```csharp
using Microsoft.Extensions.Logging;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Core.Services;

public sealed class StubResultPublisher : IResultPublisher
{
    private readonly ILogger<StubResultPublisher> _logger;

    public StubResultPublisher(ILogger<StubResultPublisher> logger)
    {
        _logger = logger;
    }

    public Task Publish(AgentRunSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        _logger.LogInformation(
            "Run summary: {Total} total, {Succeeded} succeeded, {TimedOut} timed out",
            summary.Total, summary.Succeeded, summary.TimedOut);
        return Task.CompletedTask;
    }
}
```

Add `Microsoft.Extensions.Logging.Abstractions` to `SampleDurable.Core.csproj` if not already referenced.

- [ ] **Step 6: Register both in `AddCoreServices`**

`src/SampleDurable.Core/ServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Services;

namespace SampleDurable.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IAgentDispatcher, StubAgentDispatcher>();
        services.AddSingleton<IResultPublisher, StubResultPublisher>();
        return services;
    }
}
```

The existing `DiRegistrationTests` asserts every public `Core.Interfaces` type is registered here. Skipping either registration fails that test — this is intended.

- [ ] **Step 7: Run tests and the full gate**

Run:
```powershell
dotnet build --no-incremental
dotnet test tests/SampleDurable.Tests.Unit --filter StubAgentDispatcherTests
dotnet test
```
Expected: build green, both new tests pass, architecture tests (including `DiRegistrationTests`) still pass.

`TestCoverageAnalyzer` (CI0002) requires a test fixture per public class. If it fires on `StubResultPublisher`, add `tests/SampleDurable.Tests.Unit/Core/StubResultPublisherTests.cs` asserting `Publish` completes and the logger received one Information entry (substitute `ILogger<StubResultPublisher>` with NSubstitute).

- [ ] **Step 8: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable template Core models, interfaces, and stub services"
```

---

### Task 3: Functions host and activities

**Files:**
- Modify: `src/SampleDurable.Functions/SampleDurable.Functions.csproj`
- Create: `src/SampleDurable.Functions/Program.cs`
- Create: `host.json`, `local.settings.json`
- Modify: `.gitignore`
- Create: `src/SampleDurable.Functions/Activities/DispatchAgentActivity.cs`, `PublishSummaryActivity.cs`
- Test: `tests/SampleDurable.Tests.Unit/Functions/DispatchAgentActivityTests.cs`

**Interfaces:**
- Consumes: Task 2's `IAgentDispatcher`, `IResultPublisher`, `AgentWorkItem`, `AgentDispatch`, `AgentRunSummary`, `AddCoreServices`.
- Produces:
  - `DispatchAgentActivity.Run([ActivityTrigger] AgentWorkItem, CancellationToken) → Task<AgentDispatch>`, `[Function(nameof(DispatchAgentActivity))]`
  - `PublishSummaryActivity.Run([ActivityTrigger] AgentRunSummary, CancellationToken) → Task`, `[Function(nameof(PublishSummaryActivity))]`

- [ ] **Step 1: Write the Functions csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.7" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.DurableTask" Version="1.18.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SampleDurable.Core\SampleDurable.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="host.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="local.settings.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Never" />
  </ItemGroup>

</Project>
```

`Http.AspNetCore` 2.0.4 is a starting pin — verify against the restored package and adjust.

- [ ] **Step 2: Write `Program.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleDurable.Core;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddCoreServices();

builder.Build().Run();

public partial class Program;
```

If `FunctionsApplication.CreateBuilder` does not resolve, fall back to the `HostBuilder` form:

```csharp
using Microsoft.Extensions.Hosting;
using SampleDurable.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => services.AddCoreServices())
    .Build();

host.Run();

public partial class Program;
```

- [ ] **Step 3: Write `host.json`**

```json
{
  "version": "2.0",
  "logging": {
    "logLevel": {
      "default": "Information"
    }
  },
  "extensions": {
    "durableTask": {
      "hubName": "SampleDurableHub"
    }
  }
}
```

Schema version is `2.0` — unchanged, and unrelated to the Functions runtime major version. The default Azure Storage backend needs no `storageProvider` block.

- [ ] **Step 4: Write `local.settings.json`**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

Add to `.gitignore`:
```
local.settings.json
```

Then force-add it so the template ships it (it is scaffolding, not a secret — it holds only the Azurite development shortcut):
```bash
git add -f dotnet/templates/durable/local.settings.json
```

- [ ] **Step 5a: Extend the role-suffix vocabulary**

`DispatchAgentActivity` and `PublishSummaryActivity` are concrete public classes (they inject dependencies, so they cannot be static). `NamingConventionTests.Should_use_recognised_role_suffix_on_concrete_classes` will reject them — `Activity` is not yet in its vocabulary.

In `tests/SampleDurable.Tests.Architecture/NamingConventionTests.cs`, extend `AllowedSuffixes` with `"Activity"`, `"Orchestrator"`, and `"Trigger"`. Task 2 already appended `"Dispatcher"` and `"Publisher"`; append to the same collection:

```csharp
        "Tool", "Resource", "Prompt", "Dispatcher", "Publisher",
        "Activity", "Orchestrator", "Trigger"
```

Add all three now even though only `Activity` is strictly required this task: Task 4's orchestrators and triggers are static classes (which the rule skips, since a static class is `abstract sealed` in IL), but a forker writing a non-static trigger would hit this wall for no reason. This mirrors what the `mcp` template did with `Tool`/`Resource`/`Prompt` — each template teaches the rule its own domain vocabulary.

Extend the vocabulary; never rename a domain class to satisfy the rule.

- [ ] **Step 5: Write the failing activity test**

Create `tests/SampleDurable.Tests.Unit/Functions/DispatchAgentActivityTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;

namespace SampleDurable.Tests.Unit.Functions;

[TestFixture]
public class DispatchAgentActivityTests
{
    [Test]
    public async Task Should_return_the_dispatch_produced_by_the_dispatcher()
    {
        var dispatcher = Substitute.For<IAgentDispatcher>();
        var item = new AgentWorkItem("item-1", "do the thing");
        dispatcher
            .Dispatch(item, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentDispatch("dispatch-9", "item-1")));

        var activity = new DispatchAgentActivity(dispatcher);

        var result = await activity.Run(item, CancellationToken.None);

        result.DispatchId.Should().Be("dispatch-9");
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

Run: `dotnet test tests/SampleDurable.Tests.Unit --filter DispatchAgentActivityTests`
Expected: FAIL — `DispatchAgentActivity` does not exist (CS0246).

- [ ] **Step 7: Write the activities**

`src/SampleDurable.Functions/Activities/DispatchAgentActivity.cs`:
```csharp
using Microsoft.Azure.Functions.Worker;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Activities;

public sealed class DispatchAgentActivity
{
    private readonly IAgentDispatcher _dispatcher;

    public DispatchAgentActivity(IAgentDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Function(nameof(DispatchAgentActivity))]
    public Task<AgentDispatch> Run(
        [ActivityTrigger] AgentWorkItem workItem,
        CancellationToken cancellationToken)
        => _dispatcher.Dispatch(workItem, cancellationToken);
}
```

Constructor injection and a `CancellationToken` are both correct **here** — activities are not replayed. CI0017 and `DURABLE0007` forbid exactly this shape in orchestrators.

`src/SampleDurable.Functions/Activities/PublishSummaryActivity.cs`:
```csharp
using Microsoft.Azure.Functions.Worker;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Activities;

public sealed class PublishSummaryActivity
{
    private readonly IResultPublisher _publisher;

    public PublishSummaryActivity(IResultPublisher publisher)
    {
        _publisher = publisher;
    }

    [Function(nameof(PublishSummaryActivity))]
    public Task Run(
        [ActivityTrigger] AgentRunSummary summary,
        CancellationToken cancellationToken)
        => _publisher.Publish(summary, cancellationToken);
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run:
```powershell
dotnet build --no-incremental
dotnet test tests/SampleDurable.Tests.Unit --filter DispatchAgentActivityTests
```
Expected: PASS.

Add `tests/SampleDurable.Tests.Unit/Functions/PublishSummaryActivityTests.cs` in the same shape if `TestCoverageAnalyzer` (CI0002) demands it — substitute `IResultPublisher`, call `Run`, and assert `Publish` received the summary via `await _publisher.Received(1).Publish(summary, Arg.Any<CancellationToken>())`.

- [ ] **Step 9: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable template Functions host and activities"
```

---

### Task 4: Orchestrations, triggers, and the durable entity

The sample's centre of gravity. Deliverable: the full agent-shaped loop compiles and the whole gate is green.

**Files:**
- Create: `src/SampleDurable.Functions/Orchestrations/AgentRunOrchestrator.cs`, `AgentTaskOrchestrator.cs`
- Create: `src/SampleDurable.Functions/Entities/RunCounterEntity.cs`
- Create: `src/SampleDurable.Functions/Triggers/RunWebhookTrigger.cs`, `CallbackTrigger.cs`, `RunStatusTrigger.cs`

**Interfaces:**
- Consumes: Task 2's models; Task 3's `DispatchAgentActivity`, `PublishSummaryActivity`.
- Produces:
  - `AgentRunOrchestrator.Run([OrchestrationTrigger] TaskOrchestrationContext) → Task<AgentRunSummary>`
  - `AgentTaskOrchestrator.Run([OrchestrationTrigger] TaskOrchestrationContext) → Task<AgentResult>`
  - `AgentTaskOrchestrator.AgentCompletedEventName` — `const string` = `"AgentCompleted"`
  - `AgentTaskOrchestrator.DispatchTimeout` — `static readonly TimeSpan` = 2 hours
  - `RunCounterEntity` — `TaskEntity<RunCounterState>` with operations `Dispatched()`, `Completed()`, `TimedOut()`, `Get()`
  - Instance ID convention: `$"run-{request.RunKey}"`

- [ ] **Step 1: Write `AgentTaskOrchestrator` — the callback-or-timeout core**

`src/SampleDurable.Functions/Orchestrations/AgentTaskOrchestrator.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Entities;

namespace SampleDurable.Functions.Orchestrations;

public static class AgentTaskOrchestrator
{
    public const string AgentCompletedEventName = "AgentCompleted";

    public static readonly TimeSpan DispatchTimeout = TimeSpan.FromHours(2);

    [Function(nameof(AgentTaskOrchestrator))]
    public static async Task<AgentResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        AgentWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workItem);

        var counter = new EntityInstanceId(nameof(RunCounterEntity), context.InstanceId);

        await context.CallActivityAsync<AgentDispatch>(nameof(DispatchAgentActivity), workItem);
        await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.Dispatched));

        var completed = context.WaitForExternalEvent<AgentResult>(AgentCompletedEventName);
        var timeout = context.CreateTimer(context.CurrentUtcDateTime.Add(DispatchTimeout), CancellationToken.None);

        var winner = await Task.WhenAny(completed, timeout);

        if (winner == completed)
        {
            await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.Completed));
            return await completed;
        }

        await context.Entities.SignalEntityAsync(counter, nameof(RunCounterEntity.TimedOut));
        return new AgentResult(workItem.Id, Succeeded: false, Output: "timed out");
    }
}
```

Three things here are load-bearing and must not be "simplified":
- `context.CurrentUtcDateTime`, never `DateTime.UtcNow` — `DURABLE0001`.
- `Task.WhenAny` over two context-derived tasks is the **only** legal way to race. CI0016 must allow this — it is the false-positive canary.
- The entity signal is fire-and-forget by design; entity operations are not replayed.

The counter entity is keyed by `context.InstanceId` of the **sub**-orchestration, giving one counter per work item. If a single run-wide counter is wanted instead, pass the parent instance ID in as part of the input — do not read it from ambient state.

- [ ] **Step 2: Write `AgentRunOrchestrator` — fan-out/fan-in**

`src/SampleDurable.Functions/Orchestrations/AgentRunOrchestrator.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;

namespace SampleDurable.Functions.Orchestrations;

public static class AgentRunOrchestrator
{
    public static string SubInstanceId(string runKey, string workItemId) => $"run-{runKey}-{workItemId}";

    [Function(nameof(AgentRunOrchestrator))]
    public static async Task<AgentRunSummary> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        AgentRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var tasks = new List<Task<AgentResult>>();
        foreach (var item in request.Items)
        {
            var options = new SubOrchestrationOptions(SubInstanceId(request.RunKey, item.Id));
            tasks.Add(context.CallSubOrchestratorAsync<AgentResult>(nameof(AgentTaskOrchestrator), item, options));
        }

        var results = await Task.WhenAll(tasks);

        var summary = new AgentRunSummary(
            Total: results.Length,
            Succeeded: results.Count(r => r.Succeeded),
            TimedOut: results.Count(r => !r.Succeeded));

        await context.CallActivityAsync(nameof(PublishSummaryActivity), summary);

        return summary;
    }
}
```

`Task.WhenAll` over a `List<Task<T>>` built from `CallSubOrchestratorAsync` is **the** fan-out shape. CI0016 must not fire on it. Verify this explicitly in Task 7.

`SubInstanceId` is deliberately public and deterministic. A real callback URL has to name the specific parked sub-orchestration, and a caller cannot discover a server-generated child ID — the parent's status does not list its children. Deriving it from `(RunKey, WorkItemId)` makes every sub-orchestration addressable from outside, which is what makes `CallbackTrigger` usable at all, and it is what Task 11's integration test targets. If `SubOrchestrationOptions` is not the correct type name in the installed package, use whatever `CallSubOrchestratorAsync`'s options parameter accepts (likely `TaskOptions` with an instance-ID-carrying subclass) — keep the deterministic ID either way.

- [ ] **Step 3: Write the entity**

`src/SampleDurable.Functions/Entities/RunCounterEntity.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;

namespace SampleDurable.Functions.Entities;

public sealed class RunCounterEntity : TaskEntity<RunCounterState>
{
    protected override RunCounterState InitializeState(TaskEntityOperation operation)
        => RunCounterState.Empty;

    public void Dispatched() => State = State with { Dispatched = State.Dispatched + 1 };

    public void Completed() => State = State with { Completed = State.Completed + 1 };

    public void TimedOut() => State = State with { TimedOut = State.TimedOut + 1 };

    public RunCounterState Get() => State;

    [Function(nameof(RunCounterEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync(this);
    }
}
```

`[EntityTrigger]` must bind `TaskEntityDispatcher` — `DURABLE1003` is Error by default and will catch any other type.

- [ ] **Step 4: Write the webhook trigger with a deterministic instance ID**

`src/SampleDurable.Functions/Triggers/RunWebhookTrigger.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Functions.Triggers;

public static class RunWebhookTrigger
{
    [Function(nameof(RunWebhookTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs")] HttpRequest request,
        [DurableClient] DurableTaskClient client,
        [FromBody] AgentRunRequest body)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(body);

        var instanceId = $"run-{body.RunKey}";

        var existing = await client.GetInstanceAsync(instanceId);
        if (existing is not null)
        {
            return new OkObjectResult(new { instanceId, status = "already running" });
        }

        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(AgentRunOrchestrator),
            body,
            new StartOrchestrationOptions(instanceId));

        return await client.CreateCheckStatusResponseAsync(request, instanceId);
    }
}
```

The deterministic instance ID is the idempotency mechanism: a webhook redelivery for the same `RunKey` returns the existing run rather than starting a second one. Do not replace it with a generated ID.

If `CreateCheckStatusResponseAsync` does not accept `HttpRequest` in the ASP.NET Core integration, return `new OkObjectResult(new { instanceId })` instead and note the deviation.

- [ ] **Step 5: Write the callback trigger**

`src/SampleDurable.Functions/Triggers/CallbackTrigger.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Functions.Triggers;

public static class CallbackTrigger
{
    [Function(nameof(CallbackTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs/{instanceId}/callback")] HttpRequest request,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        [FromBody] AgentResult result)
    {
        ArgumentNullException.ThrowIfNull(client);

        await client.RaiseEventAsync(instanceId, AgentTaskOrchestrator.AgentCompletedEventName, result);

        return new AcceptedResult();
    }
}
```

This is how the outside world wakes a parked orchestration. The event name comes from the orchestrator's `const`, never a literal.

- [ ] **Step 6: Write the status trigger**

`src/SampleDurable.Functions/Triggers/RunStatusTrigger.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Entities;

namespace SampleDurable.Functions.Triggers;

public static class RunStatusTrigger
{
    [Function(nameof(RunStatusTrigger))]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs/{instanceId}/counters")] HttpRequest request,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        ArgumentNullException.ThrowIfNull(client);

        var entityId = new EntityInstanceId(nameof(RunCounterEntity), instanceId);
        var state = await client.Entities.GetEntityAsync<RunCounterState>(entityId);

        return state is null
            ? new NotFoundResult()
            : new OkObjectResult(state.State);
    }
}
```

- [ ] **Step 7: Build and run the full gate**

Run:
```powershell
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test
```
Expected: all green. **If any `DURABLE*` diagnostic fires, the sample is wrong — fix the sample, not the rule.** Unused `HttpRequest` parameters may trip IDE0060; if so, keep the parameter (the binding needs it) and suppress via `.editorconfig` scoped to `Triggers/`, documenting why.

- [ ] **Step 8: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable template orchestrations, triggers, and entity"
```

---

### Task 5: Orchestrator unit tests

Encodes the `TaskName`/extension-method gotcha so the forker inherits the correct pattern instead of discovering it.

**Files:**
- Test: `tests/SampleDurable.Tests.Unit/Functions/Orchestrations/AgentTaskOrchestratorTests.cs`, `AgentRunOrchestratorTests.cs`
- Modify: `tests/SampleDurable.Tests.Unit/SampleDurable.Tests.Unit.csproj`

**Interfaces:**
- Consumes: Task 4's orchestrators.
- Produces: the substituted-context test pattern later tasks and forkers copy.

- [ ] **Step 1: Add the DurableTask reference to the unit test project**

In `tests/SampleDurable.Tests.Unit/SampleDurable.Tests.Unit.csproj`, add to the existing `PackageReference` ItemGroup:

```xml
    <PackageReference Include="Microsoft.DurableTask.Abstractions" Version="1.24.1" />
```

Verify the version matches what `Worker.Extensions.DurableTask` 1.18.0 resolves transitively — run `dotnet list tests/SampleDurable.Tests.Unit package --include-transitive` and pin to that exact version to avoid a downgrade warning becoming an error.

- [ ] **Step 2: Write the failing test for the timeout branch**

**Both fixtures already exist** — Task 4 created them to satisfy the per-class fixture gate, and Task 4's review found the `AgentTaskOrchestratorTests` content to be tautological ballast: it asserts `AgentCompletedEventName == "AgentCompleted"` and `DispatchTimeout == TimeSpan.FromHours(2)`, i.e. a constant compared against a hand-typed copy of its own literal, never invoking `Run`. Nothing plausible breaks those assertions, and `CallbackTriggerTests` already exercises the same constant through real wiring.

**Replace that file's contents entirely** with the fixture below — do not append to it and do not preserve the two existing tests. Closing that finding is part of this task.

`AgentRunOrchestratorTests` is different: its existing `SubInstanceId` tests are load-bearing (they pin a format the callback URL and Task 11 depend on). **Keep those two tests and add** the fan-out tests from Step 5 alongside them.

Overwrite `tests/SampleDurable.Tests.Unit/Functions/Orchestrations/AgentTaskOrchestratorTests.cs` with:

```csharp
using FluentAssertions;
using Microsoft.DurableTask;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentTaskOrchestratorTests
{
    private static TaskOrchestrationContext CreateContext()
    {
        var context = Substitute.For<TaskOrchestrationContext>();
        context.CurrentUtcDateTime.Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        context.InstanceId.Returns("run-abc");
        context.Entities.Returns(Substitute.For<TaskOrchestrationEntityFeature>());
        return context;
    }

    private static void StubDispatch(TaskOrchestrationContext context)
    {
        context
            .CallActivityAsync<AgentDispatch>(
                Arg.Is<TaskName>(n => n.Name == nameof(DispatchAgentActivity)),
                Arg.Any<object>(),
                Arg.Any<TaskOptions>())
            .Returns(Task.FromResult(new AgentDispatch("d-1", "item-1")));
    }

    [Test]
    public async Task Should_return_timed_out_result_when_the_timer_wins()
    {
        var context = CreateContext();
        StubDispatch(context);

        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(new TaskCompletionSource<AgentResult>().Task);
        context
            .CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        using (new AssertionScope())
        {
            result.Succeeded.Should().BeFalse();
            result.WorkItemId.Should().Be("item-1");
            result.Output.Should().Be("timed out");
        }
    }

    [Test]
    public async Task Should_return_the_agent_result_when_the_callback_wins()
    {
        var context = CreateContext();
        StubDispatch(context);

        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(Task.FromResult(new AgentResult("item-1", true, "done")));
        context
            .CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource().Task);

        var result = await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        using (new AssertionScope())
        {
            result.Succeeded.Should().BeTrue();
            result.Output.Should().Be("done");
        }
    }

    [Test]
    public async Task Should_dispatch_before_waiting_for_the_callback()
    {
        var context = CreateContext();
        StubDispatch(context);
        context
            .WaitForExternalEvent<AgentResult>(AgentTaskOrchestrator.AgentCompletedEventName)
            .Returns(Task.FromResult(new AgentResult("item-1", true, "done")));
        context.CreateTimer(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource().Task);

        await AgentTaskOrchestrator.Run(context, new AgentWorkItem("item-1", "p"));

        await context.Received(1).CallActivityAsync<AgentDispatch>(
            Arg.Is<TaskName>(n => n.Name == nameof(DispatchAgentActivity)),
            Arg.Any<object>(),
            Arg.Any<TaskOptions>());
    }
}
```

Add `using FluentAssertions.Execution;`.

**The gotcha, stated once so every later fixture inherits it:** `CallActivityAsync` does not take a `string` — it takes a **`TaskName` struct** with an implicit conversion from string. The call site reads like a string, but a substitution matching a raw string will never match. Always use `Arg.Is<TaskName>(n => n.Name == nameof(X))`, and supply `Arg.Any<TaskOptions>()` for the options parameter. Microsoft's docs show this in xUnit + Moq form; this is the NSubstitute translation.

**Corrected during Task 5 — an earlier draft of this plan was wrong here.** It claimed the convenient overloads were "extension methods and cannot be substituted." Reflection over `Microsoft.DurableTask.Abstractions` 1.24.1 disproves it: `TaskOrchestrationContext` declares all four `CallActivityAsync` overloads **on the type itself**, every one `virtual` (the three-argument generic one additionally `abstract`). NSubstitute substitutes all of them, and `TaskOrchestrationEntityFeature` too — no hand-written double is needed. Do not reintroduce the extension-method claim into any doc; `TaskName` matching is the only real constraint here.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/SampleDurable.Tests.Unit --filter AgentTaskOrchestratorTests`
Expected: FAIL. If it fails with a substitution error on `TaskOrchestrationContext` or `TaskOrchestrationEntityFeature` (non-virtual member, or no accessible constructor), that is a **real finding**, not a test bug — record which members resist substitution and fall back to a hand-written test double deriving from `TaskOrchestrationContext` that overrides only the abstract members. Do not delete the test.

- [ ] **Step 4: Make them pass**

The orchestrator from Task 4 should already satisfy these. If a test fails on `context.Entities` being null, the `TaskOrchestrationEntityFeature` substitution in `CreateContext()` is the fix.

- [ ] **Step 5: Write the fan-out test**

**Add** these two tests to the existing `tests/SampleDurable.Tests.Unit/Functions/Orchestrations/AgentRunOrchestratorTests.cs`, keeping its two existing `SubInstanceId` tests — those pin a format the callback URL and Task 11 depend on. The fixture's final shape is those two plus these two:

```csharp
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DurableTask;
using NSubstitute;
using SampleDurable.Core.Models;
using SampleDurable.Functions.Activities;
using SampleDurable.Functions.Orchestrations;

namespace SampleDurable.Tests.Unit.Functions.Orchestrations;

[TestFixture]
public class AgentRunOrchestratorTests
{
    [Test]
    public async Task Should_aggregate_sub_orchestration_results_into_a_summary()
    {
        var context = Substitute.For<TaskOrchestrationContext>();

        context
            .CallSubOrchestratorAsync<AgentResult>(
                Arg.Is<TaskName>(n => n.Name == nameof(AgentTaskOrchestrator)),
                Arg.Any<object>(),
                Arg.Any<TaskOptions>())
            .Returns(
                Task.FromResult(new AgentResult("a", true, "ok")),
                Task.FromResult(new AgentResult("b", false, "timed out")));

        var request = new AgentRunRequest("key-1",
        [
            new AgentWorkItem("a", "p"),
            new AgentWorkItem("b", "p")
        ]);

        var summary = await AgentRunOrchestrator.Run(context, request);

        using (new AssertionScope())
        {
            summary.Total.Should().Be(2);
            summary.Succeeded.Should().Be(1);
            summary.TimedOut.Should().Be(1);
        }
    }

    [Test]
    public async Task Should_publish_the_summary()
    {
        var context = Substitute.For<TaskOrchestrationContext>();
        context
            .CallSubOrchestratorAsync<AgentResult>(
                Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<TaskOptions>())
            .Returns(Task.FromResult(new AgentResult("a", true, "ok")));

        var request = new AgentRunRequest("key-1", [new AgentWorkItem("a", "p")]);

        await AgentRunOrchestrator.Run(context, request);

        await context.Received(1).CallActivityAsync(
            Arg.Is<TaskName>(n => n.Name == nameof(PublishSummaryActivity)),
            Arg.Any<object>(),
            Arg.Any<TaskOptions>());
    }
}
```

If the non-generic `CallActivityAsync` is an extension method over `CallActivityAsync<object>`, assert on the generic form instead — check the abstraction's surface and adjust rather than asserting on something unsubstitutable.

- [ ] **Step 6: Run the full gate**

Run:
```powershell
dotnet build --no-incremental
dotnet test
```
Expected: all green.

- [ ] **Step 7: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable template orchestrator unit tests"
```

---

### Task 6: Pin Microsoft's DURABLE analyzers to error

**Files:**
- Modify: `dotnet/templates/durable/.editorconfig`

**Interfaces:**
- Consumes: Task 4's sample (must stay green under the pins).
- Produces: `.editorconfig` severity pins.

- [ ] **Step 1: Confirm the analyzers are already active**

Run, from a scaffolded project:
```powershell
dotnet build --no-incremental -v n 2>&1 | Select-String 'DURABLE'
```
Expected: no output on the clean sample. The `DURABLE*` analyzers ship **inside** `Worker.Extensions.DurableTask` v1.6.0+ and are on by default — if adding a package reference for them seems necessary, that is a misunderstanding; do not add `Microsoft.DurableTask.Analyzers`.

- [ ] **Step 2: Write the failing check — seed a determinism violation**

Temporarily edit `AgentTaskOrchestrator.Run`, replacing `context.CurrentUtcDateTime` with `DateTime.UtcNow`:

```csharp
        var timeout = context.CreateTimer(DateTime.UtcNow.Add(DispatchTimeout), CancellationToken.None);
```

Run: `dotnet build --no-incremental`
Expected: **FAIL** with `DURABLE0001`. It already breaks the build via `TreatWarningsAsErrors` even before the pins — confirm this, then revert the edit.

- [ ] **Step 3: Add the severity pins**

Append to `dotnet/templates/durable/.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.DURABLE0001.severity = error
dotnet_diagnostic.DURABLE0002.severity = error
dotnet_diagnostic.DURABLE0003.severity = error
dotnet_diagnostic.DURABLE0004.severity = error
dotnet_diagnostic.DURABLE0005.severity = error
dotnet_diagnostic.DURABLE0006.severity = error
dotnet_diagnostic.DURABLE0007.severity = error
dotnet_diagnostic.DURABLE0008.severity = error
dotnet_diagnostic.DURABLE0010.severity = error
dotnet_diagnostic.DURABLE0011.severity = error
dotnet_diagnostic.DURABLE2003.severity = error
dotnet_diagnostic.DURABLE2004.severity = error
```

Insert into the existing `[*.cs]` section if one exists rather than adding a duplicate header. `DURABLE0009` is deliberately absent — it is a style preference (input parameter vs `GetInput<T>()`), not a correctness rule. `DURABLE1001`–`1003` are Error by default and need no pin.

These pins are belt-and-braces: `TreatWarningsAsErrors` already promotes them. They exist so the intent survives someone relaxing that flag.

- [ ] **Step 4: Verify the sample is still green under the pins**

Run:
```powershell
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test
```
Expected: all green. `DURABLE2003`/`2004` promoted to error is the riskiest pin — if either fires on the sample's `nameof`-based calls, the rule cannot resolve them; drop those two pins to `warning` and note why in the commit message.

- [ ] **Step 5: Commit**

```bash
git add dotnet/templates/durable/.editorconfig
git commit -m "Pin DURABLE determinism analyzers to error severity"
```

---

### Task 7: CI0016 — OrchestratorNonDurableAwait

The headline rule and the hardest. `DURABLE0005` only detects a fixed list of known I/O types, so `await _agentClient.DispatchAsync()` in an orchestrator is invisible to it and silently corrupts replay. CI0016 inverts the logic: allowlist what is provably safe, error on everything else.

**Files:**
- Create: `src/SampleDurable.Analyzers/DurableAnalyzerConstants.cs`
- Create: `src/SampleDurable.Analyzers/OrchestratorNonDurableAwaitAnalyzer.cs`
- Create: `tests/SampleDurable.Tests.Analyzers/DurableTestSources.cs`
- Create: `tests/SampleDurable.Tests.Analyzers/OrchestratorNonDurableAwaitAnalyzerTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks (the analyzer project has no project references).
- Produces:
  - `DurableAnalyzerConstants.OrchestrationTriggerAttribute` = `"Microsoft.Azure.Functions.Worker.OrchestrationTriggerAttribute"`
  - `DurableAnalyzerConstants.TaskOrchestrationContextType` = `"Microsoft.DurableTask.TaskOrchestrationContext"`
  - `DurableAnalyzerConstants.IsOrchestratorMethod(IMethodSymbol) → bool`
  - `OrchestratorNonDurableAwaitAnalyzer.DiagnosticId` = `"CI0016"`
  - `DurableTestSources.Stubs` — a source string later analyzer fixtures reuse

- [ ] **Step 1: Write the shared constants**

`src/SampleDurable.Analyzers/DurableAnalyzerConstants.cs`:

```csharp
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SampleDurable.Analyzers;

internal static class DurableAnalyzerConstants
{
    internal const string OrchestrationTriggerAttribute =
        "Microsoft.Azure.Functions.Worker.OrchestrationTriggerAttribute";

    internal const string TaskOrchestrationContextType =
        "Microsoft.DurableTask.TaskOrchestrationContext";

    internal const string DurableNamespacePrefix = "Microsoft.DurableTask";

    internal static bool IsOrchestratorMethod(IMethodSymbol? method)
    {
        if (method == null)
        {
            return false;
        }

        return method.Parameters.Any(HasOrchestrationTrigger);
    }

    private static bool HasOrchestrationTrigger(IParameterSymbol parameter) =>
        parameter.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == OrchestrationTriggerAttribute);

    internal static bool IsOrchestrationContext(ITypeSymbol? type) =>
        type?.ToDisplayString() == TaskOrchestrationContextType;

    internal static bool IsDurableType(ITypeSymbol? type)
    {
        var ns = type?.ContainingNamespace?.ToDisplayString();
        if (ns == null)
        {
            return false;
        }

        return ns == DurableNamespacePrefix
            || ns.StartsWith(DurableNamespacePrefix + ".", System.StringComparison.Ordinal);
    }
}
```

`IsDurableType` matches the whole `Microsoft.DurableTask.*` namespace, not just `TaskOrchestrationContext`. This is load-bearing: `context.Entities.SignalEntityAsync(...)` is a durable operation, but its containing type is `Microsoft.DurableTask.Entities.TaskOrchestrationEntityFeature` — a type-exact check would make CI0016 fire on the sample's own entity signal. The same applies to any future context sub-feature. `IsOrchestrationContext` stays type-exact because CI0018 needs to match `CallActivityAsync` on the context specifically.

- [ ] **Step 2: Write the failing tests**

First, the shared stub sources. Create `tests/SampleDurable.Tests.Analyzers/DurableTestSources.cs`:

```csharp
namespace SampleDurable.Tests.Analyzers;

internal static class DurableTestSources
{
    internal const string Stubs = @"
namespace Microsoft.Azure.Functions.Worker
{
    using System;
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OrchestrationTriggerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ActivityTriggerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FunctionAttribute : Attribute { public FunctionAttribute(string name) { } }
}

namespace Microsoft.DurableTask.Entities
{
    using System.Threading.Tasks;

    public readonly struct EntityInstanceId
    {
        public EntityInstanceId(string name, string key) { Name = name; Key = key; }
        public string Name { get; }
        public string Key { get; }
    }

    public abstract class TaskOrchestrationEntityFeature
    {
        public abstract Task SignalEntityAsync(EntityInstanceId id, string operationName, object input = null);
    }
}

namespace Microsoft.DurableTask
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.DurableTask.Entities;

    public readonly struct TaskName
    {
        public TaskName(string name) { Name = name; }
        public string Name { get; }
        public static implicit operator TaskName(string name) => new TaskName(name);
    }

    public class TaskOptions { }

    public abstract class TaskOrchestrationContext
    {
        public abstract string InstanceId { get; }
        public abstract DateTime CurrentUtcDateTime { get; }
        public abstract TaskOrchestrationEntityFeature Entities { get; }
        public abstract Task<T> CallActivityAsync<T>(TaskName name, object input = null, TaskOptions options = null);
        public abstract Task<T> CallSubOrchestratorAsync<T>(TaskName name, object input = null, TaskOptions options = null);
        public abstract Task<T> WaitForExternalEvent<T>(string eventName);
        public abstract Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken);
        public abstract Guid NewGuid();
    }
}
";
}
```

**Why stubs rather than real assembly references:** the analyzer matches types by fully-qualified display name, so a stub in the exact namespace exercises the identical code path, and the test stays fast and decoupled from package versions. If a rule ever depends on real API *shape* rather than names, switch that fixture to `TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(TaskOrchestrationContext).Assembly.Location))` and add the `Microsoft.DurableTask.Abstractions` package to the test project.

Create `tests/SampleDurable.Tests.Analyzers/OrchestratorNonDurableAwaitAnalyzerTests.cs`:

```csharp
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
```

The four `Should_not_report_*` tests are the false-positive canaries. They matter more than the two positive tests — a rule that fires on the fan-out, race, or entity-signal shapes is worse than no rule, because it forces the forker to disable it.

`Should_not_report_await_on_an_entity_signal` guards a specific trap: `SignalEntityAsync` lives on `TaskOrchestrationEntityFeature`, **not** on `TaskOrchestrationContext`. An `IsDurableReceiver` written as a type-exact check against `TaskOrchestrationContext` passes every other test in this fixture and still fires on the sample's own entity signal. That is why the check is namespace-based.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter OrchestratorNonDurableAwaitAnalyzerTests`
Expected: FAIL — `OrchestratorNonDurableAwaitAnalyzer` does not exist (CS0246).

- [ ] **Step 4: Write the analyzer**

`src/SampleDurable.Analyzers/OrchestratorNonDurableAwaitAnalyzer.cs`:

```csharp
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OrchestratorNonDurableAwaitAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0016";

    private static readonly ImmutableHashSet<string> AllowedTaskCombinators =
        ImmutableHashSet.Create("WhenAll", "WhenAny", "FromResult", "CompletedTask", "Delay");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Non-durable await in orchestrator",
        "'{0}' is not a durable operation — orchestrators may only await TaskOrchestrationContext calls, Task.WhenAll, or Task.WhenAny",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Awaiting anything other than a durable operation breaks orchestration replay.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
    }

    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;

        var enclosingMethod = awaitExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (enclosingMethod == null)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(enclosingMethod);
        if (!DurableAnalyzerConstants.IsOrchestratorMethod(methodSymbol))
        {
            return;
        }

        if (IsDurableExpression(awaitExpression.Expression, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, awaitExpression.Expression.GetLocation(),
                Describe(awaitExpression.Expression)));
    }

    private static bool IsDurableExpression(ExpressionSyntax expression, SemanticModel model)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                return IsDurableInvocation(invocation, model);

            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
                return IsDurableLocal(expression, model);

            case ParenthesizedExpressionSyntax parenthesized:
                return IsDurableExpression(parenthesized.Expression, model);

            default:
                return false;
        }
    }

    private static bool IsDurableInvocation(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
        {
            return false;
        }

        if (IsAllowedTaskCombinator(symbol))
        {
            return true;
        }

        return IsDurableReceiver(symbol);
    }

    private static bool IsAllowedTaskCombinator(IMethodSymbol symbol)
    {
        var containing = symbol.ContainingType?.ToDisplayString();
        if (containing != "System.Threading.Tasks.Task")
        {
            return false;
        }

        return AllowedTaskCombinators.Contains(symbol.Name);
    }

    private static bool IsDurableReceiver(IMethodSymbol symbol)
    {
        if (symbol.IsExtensionMethod && DurableAnalyzerConstants.IsDurableType(symbol.ReceiverType))
        {
            return true;
        }

        var reduced = symbol.ReducedFrom ?? symbol;
        if (reduced.IsExtensionMethod
            && reduced.Parameters.Length > 0
            && DurableAnalyzerConstants.IsDurableType(reduced.Parameters[0].Type))
        {
            return true;
        }

        return DurableAnalyzerConstants.IsDurableType(symbol.ContainingType);
    }

    private static bool IsDurableLocal(ExpressionSyntax expression, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is not ILocalSymbol local)
        {
            return false;
        }

        var declarator = local.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var initializer = declarator?.Initializer?.Value;
        return initializer != null && IsDurableExpression(initializer, model);
    }

    private static string Describe(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        return text.Length > 60 ? text.Substring(0, 57) + "..." : text;
    }
}
```

`IsDurableLocal` is what makes `var completed = context.WaitForExternalEvent<T>(...); ... await completed;` legal — the race pattern awaits a **local**, not an invocation. Without it the rule fires on its own sample. `Task.Delay` is in the combinator allowlist only because `DURABLE0003` already bans it with a better message; double-reporting helps nobody.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter OrchestratorNonDurableAwaitAnalyzerTests`
Expected: PASS, all six.

If the two `Should_not_report_*` combinator tests fail, the rule is too aggressive — fix the analyzer, never the canary test. If `Should_report_await_on_a_static_io_call` fails because `HttpClient` is unavailable in the default `ReferenceAssemblies`, set `ReferenceAssemblies = ReferenceAssemblies.Net.Net80` on the test (or the newest `Net*` the testing package exposes) rather than deleting the case.

- [ ] **Step 6: Verify the rule holds against the real sample**

Run:
```powershell
dotnet build --no-incremental
```
Expected: green — CI0016 must **not** fire on `AgentRunOrchestrator`'s `Task.WhenAll` or `AgentTaskOrchestrator`'s `Task.WhenAny`. This is the real canary; the unit tests only approximate it.

Then seed a violation — add to `AgentTaskOrchestrator.Run`:
```csharp
        await Task.Yield();
```
Run `dotnet build --no-incremental`. Expected: **FAIL with CI0016**. Revert.

- [ ] **Step 7: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add CI0016 analyzer banning non-durable awaits in orchestrators"
```

---

### Task 8: CI0017 — OrchestratorDependency

Catches the synchronous leak CI0016 cannot see: an orchestrator class holding an injected service and calling it without `await`.

**Files:**
- Create: `src/SampleDurable.Analyzers/OrchestratorDependencyAnalyzer.cs`
- Create: `tests/SampleDurable.Tests.Analyzers/OrchestratorDependencyAnalyzerTests.cs`

**Interfaces:**
- Consumes: Task 7's `DurableAnalyzerConstants`, `DurableTestSources.Stubs`.
- Produces: `OrchestratorDependencyAnalyzer.DiagnosticId` = `"CI0017"`.

- [ ] **Step 1: Write the failing tests**

`tests/SampleDurable.Tests.Analyzers/OrchestratorDependencyAnalyzerTests.cs`:

```csharp
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
}
```

`const` and `static readonly` members must stay legal — `AgentTaskOrchestrator` has both (`AgentCompletedEventName`, `DispatchTimeout`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter OrchestratorDependencyAnalyzerTests`
Expected: FAIL — `OrchestratorDependencyAnalyzer` does not exist.

- [ ] **Step 3: Write the analyzer**

`src/SampleDurable.Analyzers/OrchestratorDependencyAnalyzer.cs`:

```csharp
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OrchestratorDependencyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0017";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Orchestrator class has instance state",
        "'{0}' declares an orchestrator but holds instance state — orchestrators must be stateless and reach the outside world only through activities",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injected dependencies let an orchestrator perform non-replayable work.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol == null || !DeclaresOrchestrator(symbol))
        {
            return;
        }

        if (!HasInstanceState(symbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), symbol.Name));
    }

    private static bool DeclaresOrchestrator(INamedTypeSymbol symbol) =>
        symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(DurableAnalyzerConstants.IsOrchestratorMethod);

    private static bool HasInstanceState(INamedTypeSymbol symbol)
    {
        var hasInstanceField = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared);

        var hasInstanceProperty = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => !p.IsStatic);

        var hasConstructorParameters = symbol.InstanceConstructors
            .Any(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0);

        return hasInstanceField || hasInstanceProperty || hasConstructorParameters;
    }
}
```

`!f.IsConst` keeps `AgentCompletedEventName` legal; `!f.IsStatic` keeps `DispatchTimeout` legal.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter OrchestratorDependencyAnalyzerTests`
Expected: PASS, all four.

- [ ] **Step 5: Verify against the real sample and seed a violation**

Run `dotnet build --no-incremental` — expected green (both sample orchestrators are `static class`).

Then temporarily convert `AgentTaskOrchestrator` to a non-static class with `private readonly IAgentDispatcher _dispatcher;` and a constructor. Run `dotnet build --no-incremental`. Expected: **FAIL with CI0017**. Revert.

- [ ] **Step 6: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add CI0017 analyzer banning instance state on orchestrator classes"
```

---

### Task 9: CI0018 — DurableFunctionNameLiteral

**Files:**
- Create: `src/SampleDurable.Analyzers/DurableFunctionNameLiteralAnalyzer.cs`
- Create: `tests/SampleDurable.Tests.Analyzers/DurableFunctionNameLiteralAnalyzerTests.cs`

**Interfaces:**
- Consumes: Task 7's `DurableAnalyzerConstants`, `DurableTestSources.Stubs`.
- Produces: `DurableFunctionNameLiteralAnalyzer.DiagnosticId` = `"CI0018"`.

- [ ] **Step 1: Write the failing tests**

`tests/SampleDurable.Tests.Analyzers/DurableFunctionNameLiteralAnalyzerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter DurableFunctionNameLiteralAnalyzerTests`
Expected: FAIL — analyzer does not exist.

- [ ] **Step 3: Write the analyzer**

`src/SampleDurable.Analyzers/DurableFunctionNameLiteralAnalyzer.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleDurable.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DurableFunctionNameLiteralAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CI0018";

    private static readonly ImmutableHashSet<string> TargetMethods =
        ImmutableHashSet.Create("CallActivityAsync", "CallSubOrchestratorAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Durable function name is a string literal",
        "'{0}' takes a string literal — use nameof(...) or a const so renames stay safe",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => memberAccess.Name.Identifier.Text
        };

        if (!TargetMethods.Contains(methodName))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (!DurableAnalyzerConstants.IsOrchestrationContext(receiverType))
        {
            return;
        }

        var firstArgument = invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].Expression
            : null;

        if (firstArgument is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, literal.GetLocation(), methodName));
    }
}
```

Only literals are reported. `nameof(X)` is an `InvocationExpressionSyntax` and a `const` reference is an `IdentifierNameSyntax` — neither is a `LiteralExpressionSyntax`, so both pass without special-casing. `WaitForExternalEvent` and `RaiseEventAsync` are deliberately out of scope: event names have no function to `nameof`, and the sample keeps them in a `const` by convention.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SampleDurable.Tests.Analyzers --filter DurableFunctionNameLiteralAnalyzerTests`
Expected: PASS, all four.

- [ ] **Step 5: Verify against the real sample and seed a violation**

Run `dotnet build --no-incremental` — expected green (the sample uses `nameof` everywhere).

Then temporarily change `AgentRunOrchestrator` to `context.CallActivityAsync("PublishSummaryActivity", summary)`. Run `dotnet build --no-incremental`. Expected: **FAIL with CI0018**. Revert.

- [ ] **Step 6: Run the full gate and commit**

```powershell
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test
```

```bash
git add dotnet/templates/durable
git commit -m "Add CI0018 analyzer requiring nameof for durable function names"
```

---

### Task 10: Durable architecture tests

The structural counterpart to CI0017: an orchestrator physically cannot reach a service to call.

**Files:**
- Create: `tests/SampleDurable.Tests.Architecture/DurableFunctionTests.cs`
- Modify: `tests/SampleDurable.Tests.Architecture/TestHelpers.cs`

**Interfaces:**
- Consumes: Task 1's `TestHelpers.FunctionsAssembly`, `TestHelpers.CoreAssembly`; Task 4's namespaces.
- Produces: the `DurableFunctionTests` fixture.

- [ ] **Step 1: Write the failing tests**

`tests/SampleDurable.Tests.Architecture/DurableFunctionTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Functions.Worker;
using NetArchTest.Rules;

namespace SampleDurable.Tests.Architecture;

[TestFixture]
public class DurableFunctionTests
{
    [Test]
    public void Should_keep_orchestrations_free_of_core_behaviour()
    {
        Types.InAssembly(TestHelpers.FunctionsAssembly)
            .That().ResideInNamespaceContaining("Functions.Orchestrations")
            .ShouldNot().HaveDependencyOnAny(
                "SampleDurable.Core.Interfaces",
                "SampleDurable.Core.Services")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Orchestrations may see data (Core.Models) but never behaviour — reaching a service from an orchestrator breaks replay");
    }

    [Test]
    public void Should_confine_each_trigger_type_to_its_own_namespace()
    {
        var violations = new List<string>();

        foreach (var method in TestHelpers.FunctionMethods())
        {
            var ns = method.DeclaringType?.Namespace ?? string.Empty;

            foreach (var (attribute, expected) in TestHelpers.TriggerNamespaces)
            {
                var hasTrigger = method.GetParameters()
                    .Any(p => p.GetCustomAttributes()
                        .Any(a => a.GetType().Name == attribute));

                if (hasTrigger && !ns.EndsWith(expected, StringComparison.Ordinal))
                {
                    violations.Add($"{method.DeclaringType?.Name}.{method.Name} has {attribute} but lives in {ns}, expected a namespace ending in {expected}");
                }
            }
        }

        violations.Should().BeEmpty(
            "each trigger type belongs in its own folder so the guardrails and the reader can find them");
    }

    [Test]
    public void Should_declare_at_least_one_of_each_trigger_type()
    {
        var methods = TestHelpers.FunctionMethods().ToList();

        bool HasTrigger(string attribute) => methods.Any(m =>
            m.GetParameters().Any(p => p.GetCustomAttributes().Any(a => a.GetType().Name == attribute)));

        using (new AssertionScope())
        {
            HasTrigger("OrchestrationTriggerAttribute").Should().BeTrue();
            HasTrigger("ActivityTriggerAttribute").Should().BeTrue();
            HasTrigger("EntityTriggerAttribute").Should().BeTrue();
            HasTrigger("HttpTriggerAttribute").Should().BeTrue();
        }
    }
}
```

- [ ] **Step 2: Add the helpers**

Append to `tests/SampleDurable.Tests.Architecture/TestHelpers.cs`:

```csharp
    public static readonly (string Attribute, string Namespace)[] TriggerNamespaces =
    [
        ("OrchestrationTriggerAttribute", "Orchestrations"),
        ("ActivityTriggerAttribute", "Activities"),
        ("EntityTriggerAttribute", "Entities"),
        ("HttpTriggerAttribute", "Triggers")
    ];

    public static IEnumerable<MethodInfo> FunctionMethods() =>
        FunctionsAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "FunctionAttribute"));
```

Add the `Tests.Architecture` project a `ProjectReference` to `SampleDurable.Functions` if Step 8 of Task 1 did not already, and a `PackageReference` to `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` if the attribute types do not resolve.

`NoTupleReturnAnalyzer` (CI0004) bans tuple returns from public APIs — `TriggerNamespaces` is a public static field of tuple type, not a return, so it should pass. If CI0004 fires, replace the tuple with a small `internal record TriggerRule(string Attribute, string Namespace)`.

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test tests/SampleDurable.Tests.Architecture --filter DurableFunctionTests`
Expected: PASS, all three.

- [ ] **Step 4: Seed violations to prove the rules bite**

Temporarily add to `AgentRunOrchestrator`:
```csharp
using SampleDurable.Core.Interfaces;
```
plus a field of type `IAgentDispatcher`. Run `dotnet test tests/SampleDurable.Tests.Architecture --filter Should_keep_orchestrations_free_of_core_behaviour`.
Expected: **FAIL**. (CI0017 will also fire at build time — that is the belt-and-braces working.) Revert.

Then temporarily move `DispatchAgentActivity.cs` to `Orchestrations/` and change its namespace. Run the trigger-namespace test. Expected: **FAIL**. Revert.

- [ ] **Step 5: Run the full gate and commit**

```powershell
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test
```

```bash
git add dotnet/templates/durable
git commit -m "Add durable architecture tests confining orchestrations to Core.Models"
```

---

### Task 11: Integration tests against Azurite

The only thing that proves an orchestration actually replays.

**Files:**
- Modify: `tests/SampleDurable.Tests.Integration/SampleDurable.Tests.Integration.csproj`
- Create: `tests/SampleDurable.Tests.Integration/FunctionHostFixture.cs`
- Create: `tests/SampleDurable.Tests.Integration/OrchestrationLifecycleTests.cs`
- Modify: `dotnet/templates/durable/setup.ps1`

**Interfaces:**
- Consumes: Task 4's HTTP routes (`POST /api/runs`, `POST /api/runs/{instanceId}/callback`).
- Produces: `FunctionHostFixture` with `BaseAddress` and NUnit `[OneTimeSetUp]`/`[OneTimeTearDown]`.

- [ ] **Step 1: Strip the MCP packages from the integration csproj**

Remove `ModelContextProtocol`, `Microsoft.EntityFrameworkCore.Sqlite`, `SQLitePCLRaw.bundle_e_sqlite3`, `Microsoft.AspNetCore.Mvc.Testing`, and `Microsoft.AspNetCore.TestHost` — none apply. Keep `coverlet.collector`, `FluentAssertions`, `Microsoft.NET.Test.Sdk`, NUnit trio, `NSubstitute`. Add nothing; the fixture drives `func` over HTTP with `HttpClient`.

- [ ] **Step 2: Write the host fixture**

`tests/SampleDurable.Tests.Integration/FunctionHostFixture.cs`:

```csharp
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;

namespace SampleDurable.Tests.Integration;

[SetUpFixture]
public class FunctionHostFixture
{
    private const int HostPort = 7099;
    private const int AzuriteTablePort = 10002;

    private static Process? _host;

    public static Uri BaseAddress { get; } = new($"http://localhost:{HostPort}/api/");

    [OneTimeSetUp]
    public async Task StartHost()
    {
        if (!await PortIsOpen(AzuriteTablePort))
        {
            Assert.Fail(
                $"Azurite is not listening on {AzuriteTablePort}. Durable Functions needs blob (10000), " +
                "queue (10001) and table (10002). Start it with 'azurite --silent --inMemoryPersistence' " +
                "and re-run. See README.md.");
        }

        var projectDir = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SampleDurable.Functions"));

        _host = Process.Start(new ProcessStartInfo("func", $"start --port {HostPort} --csharp")
        {
            WorkingDirectory = projectDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        if (_host is null)
        {
            Assert.Fail("Could not start Azure Functions Core Tools ('func'). Install v4 and ensure it is on PATH.");
        }

        await WaitForHost();
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

    private static async Task WaitForHost()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        var deadline = DateTime.UtcNow.AddMinutes(2);

        while (DateTime.UtcNow < deadline)
        {
            if (await PortIsOpen(HostPort))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"Functions host did not start on port {HostPort} within 2 minutes.");
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
```

`[SetUpFixture]` with no namespace-level `[assembly:]` scoping applies to its own namespace — placing it in `SampleDurable.Tests.Integration` covers every fixture in that namespace. The path arithmetic to `projectDir` is fragile; if it misses, compute it from `AppContext.BaseDirectory` and walk up to the directory containing the `.slnx`.

**`Assert.Fail`, never `Assert.Ignore`** — `NoAssertIgnoreAnalyzer` (CI0011) blocks `Assert.Ignore` at error severity, and a test that silently skips reads as passing. A missing dependency must be loud.

- [ ] **Step 3: Write the lifecycle tests**

`tests/SampleDurable.Tests.Integration/OrchestrationLifecycleTests.cs`:

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using FluentAssertions;
using SampleDurable.Core.Models;

namespace SampleDurable.Tests.Integration;

[TestFixture]
public class OrchestrationLifecycleTests
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = new HttpClient { BaseAddress = FunctionHostFixture.BaseAddress };

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Should_complete_the_run_when_every_work_item_calls_back()
    {
        var runKey = $"key-{Guid.NewGuid():N}";
        var request = new AgentRunRequest(runKey, [new AgentWorkItem("item-1", "do it")]);

        var start = await _client.PostAsJsonAsync("runs", request);
        start.IsSuccessStatusCode.Should().BeTrue();

        var subInstanceId = AgentRunOrchestrator.SubInstanceId(runKey, "item-1");
        await WaitForStatus(subInstanceId, "Running");

        var callback = await _client.PostAsJsonAsync(
            $"runs/{subInstanceId}/callback",
            new AgentResult("item-1", true, "done"));
        callback.IsSuccessStatusCode.Should().BeTrue();

        var status = await WaitForStatus($"run-{runKey}", "Completed");
        var summary = status.Output.Deserialize<AgentRunSummary>(JsonOptions);

        using (new AssertionScope())
        {
            summary!.Total.Should().Be(1);
            summary.Succeeded.Should().Be(1);
            summary.TimedOut.Should().Be(0);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed record InstanceStatus(string RuntimeStatus, JsonElement Output);

    private async Task<InstanceStatus> WaitForStatus(string instanceId, string expected)
    {
        var uri = new Uri(
            FunctionHostFixture.BaseAddress,
            $"../runtime/webhooks/durabletask/instances/{instanceId}");

        var deadline = DateTime.UtcNow.AddSeconds(60);
        var last = "(never responded)";

        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<InstanceStatus>(JsonOptions);
                if (status is not null)
                {
                    last = status.RuntimeStatus;
                    if (string.Equals(last, expected, StringComparison.Ordinal))
                    {
                        return status;
                    }

                    if (last is "Failed" or "Terminated")
                    {
                        Assert.Fail($"Instance {instanceId} reached terminal status {last}, expected {expected}.");
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"Instance {instanceId} did not reach {expected} within 60s. Last status: {last}.");
        return null!;
    }

    [Test]
    public async Task Should_return_the_existing_run_for_a_duplicate_webhook_delivery()
    {
        var runKey = $"key-{Guid.NewGuid():N}";
        var request = new AgentRunRequest(runKey, [new AgentWorkItem("item-1", "do it")]);

        var first = await _client.PostAsJsonAsync("runs", request);
        var second = await _client.PostAsJsonAsync("runs", request);

        using (new AssertionScope())
        {
            first.IsSuccessStatusCode.Should().BeTrue();
            second.IsSuccessStatusCode.Should().BeTrue();
            (await second.Content.ReadAsStringAsync()).Should().Contain("already running");
        }
    }
}
```

Add `using System.Text.Json;`, `using System.Text.Json.Serialization;`, `using FluentAssertions.Execution;`, and `using SampleDurable.Functions.Orchestrations;`.

The status route is the durable extension's built-in management endpoint, which sits at `/runtime/webhooks/durabletask/instances/{id}` — **outside** the `/api/` prefix, hence the `../` in the relative URI. `runtimeStatus` values are the standard set (`Pending`, `Running`, `Completed`, `Failed`, `Terminated`, `Suspended`).

**Verify the payload shape against a live run before trusting it** (`func start`, POST a run, `curl` the status URI). The property names above are the documented ones, but confirm rather than assume — if they differ, fix `InstanceStatus`, not the test's intent.

The sub-orchestration ID comes from `AgentRunOrchestrator.SubInstanceId` (Task 4). The parent's status does not enumerate children, so a server-generated child ID would be undiscoverable from a test — and from a real callback URL. The deterministic ID is what makes both work.

The **timeout-path** test is deliberately absent: `DispatchTimeout` is 2 hours, and a test cannot wait. Do not shorten the production timeout to make it testable. Either drive the timeout through configuration and set it low in `local.settings.json` for the test run, or leave the path covered by Task 5's unit test (which is genuine coverage — the race logic is unit-testable; only the durable timer's real firing is not). **Prefer the unit test; do not add a sleeping integration test.**

- [ ] **Step 4: Run the tests locally**

```powershell
npm install -g azurite
Start-Process azurite -ArgumentList '--silent','--inMemoryPersistence'
npm install -g azure-functions-core-tools@4
dotnet test tests/SampleDurable.Tests.Integration
```
Expected: PASS. If `func start` cannot find the worker, confirm `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` in `local.settings.json` and that the project built first.

**If host startup proves flaky after a genuine attempt**, apply the spec's documented fallback: put these tests behind `[Category("RequiresAzurite")]`, exclude that category from the default run via `tests/.runsettings`, and run them explicitly in CI. Falling back is acceptable; a silently-skipping test is not.

- [ ] **Step 5: Make setup.ps1 check for Azurite**

Append to `dotnet/templates/durable/setup.ps1`, before the "Done." block:

```powershell
$azurite = Get-Command azurite -ErrorAction SilentlyContinue
if (-not $azurite) {
    Write-Host ""
    Write-Host "NOTE: Azurite was not found on PATH." -ForegroundColor Yellow
    Write-Host "Durable Functions needs it for local runs and for the integration tests."
    Write-Host "Install and start it with:"
    Write-Host ""
    Write-Host "  npm install -g azurite"
    Write-Host "  azurite --silent --inMemoryPersistence"
    Write-Host ""
    Write-Host "Unit and architecture tests (what the pre-commit hook runs) do not need it."
}
```

Check and inform; never install. Silent global npm installs from a scaffold script are not acceptable.

- [ ] **Step 6: Commit**

```bash
git add dotnet/templates/durable
git commit -m "Add durable integration tests driving the Functions host against Azurite"
```

---

### Task 12: Wire the template into the new-project CLI and plugin

**Files:**
- Create: `new-project/handlers/dotnet-durable.ps1`
- Modify: `new-project/tests/lib-tests.ps1:14,17`
- Modify: `plugin/scripts/write-stamp.ps1:3,11-16`
- Create: `plugin/skills/new-dotnet-durable/SKILL.md`
- Modify: `plugin/template-tests/validate-plugin.ps1:35`

**Interfaces:**
- Consumes: Task 1's `dotnet new durable` template.
- Produces: type `dotnet-durable`, stamp name `durable`.

- [ ] **Step 1: Write the handler**

`new-project/handlers/dotnet-durable.ps1`:

```powershell
@{
    Type        = 'dotnet-durable'
    Description = '.NET 10 Azure Durable Functions app (isolated worker, orchestrator determinism analyzers + architecture tests)'
    StampName   = 'durable'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new durable -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new durable failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE) { throw "dotnet test failed (exit $LASTEXITCODE)" }
    }
}
```

`Verify` runs `dotnet test` unfiltered, which includes the Azurite-dependent integration tests. If Task 11 landed them ungated, scope this to the same projects the pre-commit hook uses:
```powershell
        dotnet test tests/SampleDurable.Tests.Unit tests/SampleDurable.Tests.Architecture tests/SampleDurable.Tests.Analyzers --no-build --verbosity minimal
```
Scaffolding must not require Azurite — the template is standalone. Decide based on Task 11's outcome and note the choice in the commit message.

- [ ] **Step 2: Update the dispatcher tests (exact strings)**

`new-project/tests/lib-tests.ps1` line 14 — handlers are discovered alphabetically, so `dotnet-durable` sorts between `dotnet-cli` and `dotnet-etl-api`:

```powershell
Assert (($types -join ',') -eq 'dotnet-cli,dotnet-durable,dotnet-etl-api,dotnet-mcp,expo') "discovery finds five types, got $($types -join ',')"
```

Line 17:
```powershell
foreach ($t in 'expo', 'dotnet-cli', 'dotnet-durable', 'dotnet-etl-api', 'dotnet-mcp') { Assert ($usage -match [regex]::Escape($t)) "usage lists $t" }
```

- [ ] **Step 3: Run the dispatcher tests to verify they pass**

Run: `.\new-project\tests\lib-tests.ps1`
Expected: PASS. A failure naming a different order means discovery is not alphabetical — match the actual order rather than forcing it.

- [ ] **Step 4: Update the stamp script**

`plugin/scripts/write-stamp.ps1` line 3:
```powershell
    [Parameter(Mandatory = $true)][ValidateSet('cli', 'durable', 'etl-api', 'expo-app', 'mcp')][string]$Template,
```

Lines 11–16:
```powershell
$templateDirs = @{
    'cli'      = 'dotnet/templates/cli'
    'durable'  = 'dotnet/templates/durable'
    'etl-api'  = 'dotnet/templates/etl-api'
    'expo-app' = 'expo/templates/app'
    'mcp'      = 'dotnet/templates/mcp'
}
```

- [ ] **Step 5: Write the skill**

Create `plugin/skills/new-dotnet-durable/SKILL.md`:

````markdown
---
name: new-dotnet-durable
description: Use when creating a new Azure Durable Functions app from the agent-harness template ("new durable functions app", "new durable workflow", "new orchestration", "new dotnet durable"). Scaffolds via the new-project CLI, which stamps provenance and verifies guardrails.
---

# New .NET Azure Durable Functions Project

Scaffold a .NET 10 Azure Durable Functions app (isolated worker, Azure Storage backend) by
delegating to the standalone `new-project` CLI (the single source of truth for scaffold ->
stamp -> setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `AgentRunner`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-durable <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + unit/architecture/analyzer tests). If it prints a git-identity error,
   relay the fix and stop until the user configures git, then re-run. If it notes that
   Azurite is missing, relay that too — it is a warning, not a failure: Azurite is needed
   only for local `func start` runs and the integration tests, not for the scaffold or the
   pre-commit gate.
3. **Report** what the CLI printed: the project path, that the app ships a webhook-started
   orchestration with fan-out/fan-in, an external-event-vs-timer race, and a durable entity,
   and the development lifecycle from the project's CLAUDE.md (issue -> feat branch ->
   commit -> PR).

Never hand-edit `.harness.json` — the CLI writes it.
````

The skill is a thin delegate to `new-project.ps1 dotnet-durable`. It must not reimplement scaffolding — the CLI is the single code path and must work with no agent present.

- [ ] **Step 6: Update plugin validation**

`plugin/template-tests/validate-plugin.ps1` line 35:
```powershell
    $required = @('new-dotnet-cli', 'new-dotnet-durable', 'new-dotnet-etl-api', 'new-dotnet-mcp', 'new-expo-app', 'harness-update', 'harness-report', 'harness-capture-review')
```

- [ ] **Step 7: Run the plugin tests**

Run:
```powershell
.\plugin\template-tests\validate-plugin.ps1 -RequireFull
.\plugin\template-tests\stamp-test.ps1
.\new-project\tests\lib-tests.ps1
.\new-project\tests\dispatcher-args-tests.ps1
```
Expected: all PASS.

- [ ] **Step 8: End-to-end scaffold through the CLI**

Run:
```powershell
.\new-project.ps1 dotnet-durable DurableSmoke -Dest $env:TEMP\durable-smoke
```
Expected: scaffolds, stamps `.harness.json` with `template=durable`, runs setup, verifies green.

Check the stamp: `Get-Content $env:TEMP\durable-smoke\.harness.json` should show `"template": "durable"`.

Clean up: `Remove-Item -Recurse -Force $env:TEMP\durable-smoke`

- [ ] **Step 9: Commit**

```bash
git add new-project plugin
git commit -m "Wire durable template into new-project CLI and plugin"
```

---

### Task 13: CI and documentation

**Files:**
- Modify: `.github/workflows/template-ci.yml:13,24-37`
- Modify: `dotnet/README.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: CI coverage and docs.

- [ ] **Step 1: Add `durable` to the CI matrix**

`.github/workflows/template-ci.yml` line 13:
```yaml
        template: [cli, durable, etl-api, mcp]
```

- [ ] **Step 2: Add the Azurite + Core Tools step, scoped to the durable matrix leg**

Insert after the `setup-dotnet` step in `scaffold-and-test`:

```yaml
      - uses: actions/setup-node@v4
        if: matrix.template == 'durable'
        with:
          node-version: '22'

      - name: Install Azurite and Functions Core Tools
        if: matrix.template == 'durable'
        shell: pwsh
        run: |
          npm install -g azurite
          npm install -g azure-functions-core-tools@4
          Start-Process azurite -ArgumentList '--silent','--inMemoryPersistence','--location',"$env:RUNNER_TEMP\azurite"
          $deadline = (Get-Date).AddMinutes(1)
          while ((Get-Date) -lt $deadline) {
            if (Test-NetConnection -ComputerName localhost -Port 10002 -InformationLevel Quiet) { exit 0 }
            Start-Sleep -Seconds 2
          }
          Write-Error "Azurite did not open the table port (10002) within 1 minute"
          exit 1
```

`if: matrix.template == 'durable'` keeps the other three legs from paying for this. Port **10002** is the readiness probe because Durable needs tables — probing only blob (10000) would pass while Durable still fails. This runs on `windows-latest` via npm, deliberately avoiding Docker: GitHub Actions does not support service containers on Windows runners at all, which is also why the template is on Azure Storage rather than DTS.

- [ ] **Step 3: Push and verify CI is green**

Push the branch and confirm all four matrix legs pass. Expected: `durable` builds, formats clean, and its unit + architecture + analyzer + integration tests pass.

If the integration tests are flaky in CI specifically, apply Task 11's fallback rather than adding retries.

- [ ] **Step 4: Update `dotnet/README.md`**

Three edits:

1. Heading `## The three templates` → `## The four templates`.
2. The "13 custom Roslyn analyzers (CI0001–CI0013)" bullet is already wrong before this work — there are 15. Correct it to describe **18** and the durable rules:

```markdown
- **18 custom Roslyn analyzers** (CI0001–CI0018) that fire at error severity: no comments, method length cap, constructor parameter cap, no tuple returns, no anonymous serialization, no `pragma warning disable`, no chained `null` in arguments, no `Assert.Ignore`, no `IsNotNull`-only assertions, public method count cap, public type per file, test fixture must exist for every public class, etc. The `durable` template adds three orchestrator-determinism rules (CI0016–CI0018).
```

3. Add the row to the template table:

```markdown
| `durable` | Single Azure Functions isolated host (`Functions`) sharing `Core` + `Analyzers`. 3 src + 4 test projects. | Work that outlives a process: kick off an external system, park for hours, resume when it calls back. Ships a webhook-started orchestration with fan-out/fan-in, an external-event-vs-timer race, and a durable entity. Orchestrator determinism is enforced at error severity — Microsoft's `DURABLE*` rules plus CI0016–CI0018. |
```

And extend the "Pick `cli` by default" paragraph:

```markdown
Pick `cli` by default. Move to `etl-api` when you need both an HTTP API and a worker. Move to `mcp` when the HTTP surface is an MCP server, not a REST API. Move to `durable` when the work outlives a single process — long waits on external systems, callbacks, or workflows that must survive a crash mid-run. If a `cli` worker would need to poll a database to remember where it got to, you want `durable`.
```

Also update the repo-layout block and the Development section:

```powershell
.\dotnet\template-tests\scaffold-and-build.ps1 durable
```

Note in the Development section that the `durable` template's integration tests need Azurite running.

- [ ] **Step 5: Update the root `README.md`**

Harnesses table:
```markdown
| .NET 10 | [`dotnet/`](dotnet/README.md) | `cli`, `etl-api`, `mcp`, `durable` | 18 Roslyn analyzers (CI0001–CI0018), 6 architecture-test fixtures |
```

Add `new-dotnet-durable` to the scaffolding-skills list in the plugin section.

- [ ] **Step 6: Verify the docs match reality**

Run:
```powershell
(Get-ChildItem dotnet\templates\durable\src\SampleDurable.Analyzers -Filter '*Analyzer.cs').Count
Get-ChildItem dotnet\templates\durable\src\SampleDurable.Analyzers -Recurse |
  Select-String -Pattern '"CI\d{4}"' -AllMatches |
  ForEach-Object { $_.Matches.Value } | Sort-Object -Unique
```
Expected: 18 distinct IDs, CI0001–CI0018. If the count disagrees with the README, **fix the README** — do not round.

Note the other three templates still ship 15 analyzers; the README's "18" describes the `durable` template. If that reads as misleading, say "15 shared + 3 durable-only" instead. Verify which is true before writing it.

- [ ] **Step 7: Commit**

```bash
git add .github README.md dotnet/README.md
git commit -m "Add durable template to CI matrix and documentation"
```

---

### Task 14: The template's own CLAUDE.md and README

Task 1's mechanical rename fixed namespaces but not prose — the copied `CLAUDE.md` still tells the agent it is working on an MCP server. This is the file every scaffolded project's agent reads on session start, so a stale one actively misleads.

**Files:**
- Modify: `dotnet/templates/durable/CLAUDE.md`
- Modify: `dotnet/templates/durable/README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: the per-project agent-facing docs.

- [ ] **Step 1: Confirm the staleness**

Run:
```powershell
Select-String -Path dotnet\templates\durable\CLAUDE.md -Pattern 'MCP|Server|tool|resource|prompt'
```
Expected: several hits. Every one is wrong for this template.

- [ ] **Step 2: Rewrite the Architecture section of `CLAUDE.md`**

Replace the `## Architecture` bullets with:

```markdown
- **Solution:** `SampleDurable.slnx`, .NET 10, three `src/` projects (`Core`, `Functions`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
- **`Functions`** is an Azure Functions **isolated worker** host. Orchestrations live under `Orchestrations/` (`[OrchestrationTrigger]`), activities under `Activities/` (`[ActivityTrigger]`), entities under `Entities/` (`[EntityTrigger]`), and HTTP triggers under `Triggers/` (`[HttpTrigger]` + `[DurableClient]`). Functions are discovered by attribute — no manual registration.
- **Orchestrators must be deterministic.** They are replayed from an event history on every step, so the same code runs many times and must produce the same result each time. This is not a style preference — violating it corrupts running workflows in ways that do not reproduce locally. Concretely: no `DateTime.UtcNow` (use `context.CurrentUtcDateTime`), no `Guid.NewGuid()` (use `context.NewGuid()`), no `Task.Delay` (use `context.CreateTimer`), no I/O, no injected services. **All of it is enforced at error severity** — you will not get this wrong silently.
- **Where work belongs:** anything touching the outside world goes in an **activity**, which calls a `Core` service. Orchestrators only decide *what* to call and in *what order*. Activities may inject dependencies and use `CancellationToken`; orchestrators may do neither.
- **`Core`** splits `Models/` (DTOs) from `Interfaces/` + `Services/` (behaviour). Orchestrations may depend on `Models` but **not** on `Interfaces`/`Services` — `DurableFunctionTests` enforces this structurally, so an orchestrator cannot reach a service even to call it synchronously.
- **Architecture tests** in `tests/SampleDurable.Tests.Architecture/` enforce layering, DI shape, DI wiring (every public Core interface must be registered via `AddCoreServices()`), naming, one-public-type-per-file, and the durable rules above.
- **Custom analyzers** in `src/SampleDurable.Analyzers/` enforce CI0001–CI0018. CI0016 bans awaiting anything that is not a durable operation, CI0017 bans instance state on orchestrator classes, CI0018 requires `nameof(...)` for activity and sub-orchestration names.
- **Microsoft's `DURABLE*` analyzers** ship inside the durable extension package and are pinned to `error` in `.editorconfig`. Do not add `Microsoft.DurableTask.Analyzers` — that package is for the standalone SDK and conflicts.
```

- [ ] **Step 3: Rewrite the local-run section of `CLAUDE.md`**

Add after Architecture:

```markdown
## Running locally

Durable Functions needs a storage backend. This project uses Azure Storage, emulated by
**Azurite**:

```powershell
npm install -g azurite
azurite --silent --inMemoryPersistence
func start
```

Azurite must serve blob (10000), queue (10001) **and table (10002)** — durable state lives in
tables, so a blob-only emulator will start and then fail at runtime.

Unit, architecture, and analyzer tests need none of this — and those are the three the
pre-commit hook runs, so **committing never requires Azurite**. Only
`tests/SampleDurable.Tests.Integration` does.
```

- [ ] **Step 4: Fix the pre-commit description in `CLAUDE.md`**

The copied lifecycle section says `dotnet test` runs "Unit + Architecture projects (Integration not yet wired in)". For this template the accurate statement is:

```markdown
   - `dotnet test` — Unit, Architecture, and Analyzer projects. Integration tests are excluded from the gate because they need Azurite; CI runs them.
```

Verify against `.githooks/pre-commit` before writing it — the hook filters on `-path "*Unit*" -o -path "*Arch*"`, which does **not** match `Tests.Analyzers`. Either say "Unit + Architecture" to match the hook's real behaviour, or widen the hook. **Prefer matching the hook** — do not describe behaviour the hook does not have.

- [ ] **Step 5: Rewrite the template `README.md`**

Cover: what the template is, the sample's shape (webhook → fan-out → dispatch → await callback or timeout → aggregate, plus the counter entity), the local-run instructions from Step 3, the guardrail summary, and the **DTS switch** the spec promised:

```markdown
## Switching to the Durable Task Scheduler

Azure Storage is the default backend and needs no extra packages. Microsoft recommends the
Durable Task Scheduler (DTS) for new production apps. To switch:

1. Add `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureManaged`.
2. Set `storageProvider.type` to `azureManaged` in `host.json`.
3. Point the connection string at your scheduler. DTS is managed-identity only — there are
   no keys.

Locally, DTS runs via the emulator image `mcr.microsoft.com/dts/dts-emulator` (dashboard on
:8082), which is why the template does not default to it: that would put Docker on the
critical path for `setup.ps1` and for CI, which runs on Windows runners where GitHub Actions
does not support service containers.
```

- [ ] **Step 6: Verify no MCP references survive**

Run:
```powershell
Select-String -Path dotnet\templates\durable\CLAUDE.md, dotnet\templates\durable\README.md -Pattern 'MCP|ModelContextProtocol|McpServer'
```
Expected: no output.

- [ ] **Step 7: Commit**

```bash
git add dotnet/templates/durable/CLAUDE.md dotnet/templates/durable/README.md
git commit -m "Rewrite durable template CLAUDE.md and README for the durable model"
```

---

## Verification

Before opening the PR, from a clean scaffold:

```powershell
cd C:\Users\ryan7\programming\agent-project-templates
dotnet new install .\dotnet --force
dotnet new durable -n FinalCheck -o $env:TEMP\final-check
cd $env:TEMP\final-check
dotnet build --no-incremental
dotnet format --verify-no-changes
dotnet test tests\FinalCheck.Tests.Unit tests\FinalCheck.Tests.Architecture tests\FinalCheck.Tests.Analyzers
```

All green, and `FinalCheck` appears everywhere `SampleDurable` did — no leaked template name.

Then confirm each guardrail bites. Seed each violation, run `dotnet build --no-incremental`, confirm the expected error, revert:

| Seeded violation | Expected |
|---|---|
| `DateTime.UtcNow` in `AgentTaskOrchestrator` | `DURABLE0001` |
| `Guid.NewGuid()` in an orchestrator | `DURABLE0002` |
| `await Task.Yield()` in an orchestrator | `CI0016` |
| Constructor dependency on an orchestrator class | `CI0017` |
| `CallActivityAsync("PublishSummaryActivity", ...)` | `CI0018` |
| `Orchestrations/` type referencing `Core.Interfaces` | `DurableFunctionTests` fails |
| A `//` comment anywhere | `CI0013` |

And confirm the rules do **not** misfire: `Task.WhenAll` in `AgentRunOrchestrator` and `Task.WhenAny` in `AgentTaskOrchestrator` must both build clean. A CI0016 that fires here is a bug in the analyzer.

## Notes for the implementer

- **Package versions are starting pins, not gospel.** They were current on 2026-07-15. Re-verify each against the restored package; if one does not resolve, use the latest stable that does and say so in the commit.
- **When the sample and a rule disagree, the sample is usually wrong** — that is the whole premise. The exception is CI0016 firing on `Task.WhenAll`/`Task.WhenAny`, where the analyzer is wrong. Task 7 Step 5 is explicit about this.
- **Never `Assert.Ignore`.** CI0011 blocks it at error severity, and a silently-skipping test reads as passing. Missing dependencies must `Assert.Fail` loudly.
- **The template must work with no agent present.** `new-project.ps1 dotnet-durable Foo` in a plain terminal is the primary deliverable; the skill is a thin wrapper over that same command.
- **Do not "improve" copied files in passing.** Divergence between templates is a real cost — `etl-api` is already the odd one out, and widening that is worse than leaving a vacuous EF rule in place.
