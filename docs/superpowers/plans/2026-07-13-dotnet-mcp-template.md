# .NET MCP Server Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `dotnet new mcp` template producing a .NET 10 HTTP/streamable MCP server solution (tool + resource + prompt, DI→Core layering) that passes the full house-style guardrail suite, with a real protocol integration test, wired into the `new-project` CLI, plugin skill, provenance stamp, and CI.

**Architecture:** Base on the existing `cli` template (`ConsoleApp` = Cli host + Core + Analyzers + 4 test projects). Copy it to `dotnet/templates/mcp`, rename `ConsoleApp`→`McpServer` and the host project `Cli`→`Server`, then convert the host from a console Worker to an ASP.NET Core MCP server using the `ModelContextProtocol` C# SDK. Add a Core service, the three MCP primitives, per-class unit fixtures (the guardrails require them), and a `WebApplicationFactory`-based MCP protocol integration test.

**Tech Stack:** .NET 10, `ModelContextProtocol.AspNetCore` / `ModelContextProtocol` `2.0.0-preview.2`, ASP.NET Core, NUnit + FluentAssertions/NSubstitute (as in the cli template), the repo's 13 Roslyn analyzers + 5 architecture-test fixtures.

## Global Constraints

- **Base template:** copy `dotnet/templates/cli` verbatim to `dotnet/templates/mcp`, then rename. The cli files are the content source for everything not listed as MCP-specific below.
- **Project prefix / sourceName is `SampleMcp` (NOT `McpServer`).** `dotnet new` replaces the `sourceName` as a literal substring, and `McpServer` is a substring of the SDK's own identifiers (`AddMcpServer`, `[McpServerTool]`, …) — so a sourceName of `McpServer` corrupts those SDK calls when scaffolding under any other name. The template's projects are therefore named `SampleMcp.Server` / `SampleMcp.Core` / `SampleMcp.Analyzers` / `SampleMcp.Tests.*` (with `template.json` `sourceName: "SampleMcp"`); `SampleMcp` is not a substring of any SDK token, so `AddMcpServer()` / `[McpServer*]` survive scaffolding intact. Scaffolding `dotnet new mcp -n Weather` yields `Weather.Server`, `Weather.Core`, etc.
- **Verified package versions (spiked, do not change):** Server project → `ModelContextProtocol.AspNetCore` `2.0.0-preview.2`. Integration test → `ModelContextProtocol` `2.0.0-preview.2` + `Microsoft.AspNetCore.Mvc.Testing` `10.0.9`. (These transitively pull `ModelContextProtocol.Core` and `Microsoft.Extensions.AI.Abstractions`.)
- **Verified server SDK API (spiked and compiled):**
  ```csharp
  builder.Services.AddMcpServer()
      .WithHttpTransport()
      .WithToolsFromAssembly()
      .WithResourcesFromAssembly()
      .WithPromptsFromAssembly();
  var app = builder.Build();
  app.MapMcp();
  app.Run();
  public partial class Program;
  ```
  Attributes: `[McpServerToolType]` + `[McpServerTool(Name = "...")]`; `[McpServerResourceType]` + `[McpServerResource(UriTemplate = "...", Name = "...", MimeType = "...")]`; `[McpServerPromptType]` + `[McpServerPrompt(Name = "...")]`. Parameters documented via `[System.ComponentModel.Description("...")]`. Tool/resource/prompt types are discovered from the assembly; a tool type may take **constructor-injected interfaces** (resolved from DI). The tool name is what the client calls — set it explicitly with `Name`.
- **Verified client API (spiked, test passed in-memory):**
  ```csharp
  HttpClient http = factory.CreateClient();
  var transport = new HttpClientTransport(
      new HttpClientTransportOptions { Endpoint = http.BaseAddress! },
      http, NullLoggerFactory.Instance, false);
  await using var client = await McpClient.CreateAsync(transport);
  var tools = await client.ListToolsAsync();          // IList<McpClientTool>, tool.Name
  var resources = await client.ListResourcesAsync();
  var prompts = await client.ListPromptsAsync();
  var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["name"] = "Ada" });
  ```
  `WebApplicationFactory<Program>`'s in-memory handler carries the streamable-HTTP transport — no real socket needed.
- **Guardrails the new code MUST satisfy (verified against the cli architecture tests):**
  - **No `//` comments** (NoCommentsAnalyzer). Use `[Description]` attributes for MCP metadata; no XML `///` docs are required (the cli public members have none and build green).
  - **Every public class** in the Server/Core assemblies (sealed or non-abstract; excluding `Program`, `AssemblyMarker`, `*Extensions`, records) needs a matching `<ClassName>Tests` NUnit fixture (CodeStructureTests) **in a Unit sub-namespace containing `.Server` or `.Core`** to match its source assembly, and that fixture must **invoke every public method with an assertion** (TestCoverageAnalyzer CI0002).
  - **Concrete class role suffix** (NamingConventionTests): add `"Tool"`, `"Resource"`, `"Prompt"` to `AllowedSuffixes`. The Core service uses the `"Service"` suffix.
  - **Interfaces** live in a `.Interfaces` namespace and are **registered in `AddCoreServices()`** (DiRegistrationTests).
  - **No `Async` suffix** on method names. Test methods named `Should_word_word`; test fixtures end with `Tests`; assertions via `Assert.That(...)` or FluentAssertions `.Should()`.
  - One public type per file.
- **Provenance:** the template's stamp name is `mcp` (added to `write-stamp.ps1`).
- **Verification:** every task that changes the template ends with, from the scaffolded project dir, `dotnet build --no-incremental` then `dotnet test --no-build --verbosity minimal` GREEN. Scaffold with `dotnet new install <repoRoot>/dotnet --force` then `dotnet new mcp -n Tmp -o <tempdir>`. Run dotnet via the PowerShell tool or bash. Commit from repo root `C:/Users/ryan7/programming/agent-project-templates`.

This plan edits the repo on branch `feat/dotnet-mcp-template`.

---

### Task 1: Copy the cli template to `mcp` and rename to final names

**Files:**
- Create: `dotnet/templates/mcp/**` (copy of `dotnet/templates/cli/**`, excluding `bin`/`obj`), renamed.
- Modify (within the copy): `dotnet/templates/mcp/.template.config/template.json`.

**Interfaces:** Produces the `dotnet/templates/mcp` directory: a working .NET 10 solution identical in shape to cli but with `ConsoleApp`→`McpServer` and the host project `Cli`→`Server` (still a console Worker host at this point). `dotnet new mcp` registers and scaffolds a green solution.

- [ ] **Step 1: Copy the cli template (excluding build output)**

```bash
cd C:/Users/ryan7/programming/agent-project-templates
rm -rf dotnet/templates/mcp
# copy everything except bin/obj
(cd dotnet/templates/cli && find . -type d \( -name bin -o -name obj \) -prune -o -type f -print) \
  | while read f; do mkdir -p "dotnet/templates/mcp/$(dirname "$f")"; cp "dotnet/templates/cli/$f" "dotnet/templates/mcp/$f"; done
```

- [ ] **Step 2: Rename `ConsoleApp`→`McpServer` and `Cli`→`Server` across paths and file contents**

Rename directories and files first (paths contain `ConsoleApp` and `Cli`), then replace file contents. Do it in two ordered passes: `ConsoleApp`→`McpServer`, then the host token `Cli`→`Server`.

```bash
cd C:/Users/ryan7/programming/agent-project-templates/dotnet/templates/mcp
# 2a: rename directory/file names ConsoleApp -> McpServer (deepest first)
find . -depth -name '*ConsoleApp*' | while read p; do git mv 2>/dev/null "$p" "${p//ConsoleApp/McpServer}" || mv "$p" "${p//ConsoleApp/McpServer}"; done
# 2b: rename the host project dir/files Cli -> Server (only the SampleMcp.Cli host + test refs live under names with 'Cli')
find . -depth -name '*SampleMcp.Cli*' | while read p; do mv "$p" "${p//SampleMcp.Cli/SampleMcp.Server}"; done
# 2c: replace file contents — ConsoleApp -> McpServer, then SampleMcp.Cli -> SampleMcp.Server and the bare Cli tokens the arch tests use
grep -rIl --exclude-dir=bin --exclude-dir=obj 'ConsoleApp' . | while read f; do sed -i 's/ConsoleApp/McpServer/g' "$f"; done
grep -rIl --exclude-dir=bin --exclude-dir=obj 'McpServer\.Cli\|CliAssembly\|"SampleMcp.Cli"\|Cli\b' . | while read f; do
  sed -i -e 's/McpServer\.Cli/SampleMcp.Server/g' -e 's/CliAssembly/ServerAssembly/g' "$f"; done
```

Then hand-fix the remaining `Cli`-token references in the architecture tests (the bare word `Cli` in identifiers/strings that the blanket sed above may not have fully caught). Verify by grepping — these must ALL become `Server`:

```bash
grep -rIn --exclude-dir=bin --exclude-dir=obj -w 'Cli\|cli' dotnet/templates/mcp \
  | grep -v '\.template\.config' | grep -iv 'client'
```
Expected after fixes: no matches (except any inside `template.json`, handled next, and unrelated substrings like "client"). Specifically ensure these read `Server`: `TestHelpers.ServerAssembly` (was `CliAssembly`), `Assembly.Load("SampleMcp.Server")` and the `Should_keep_cli_depending_only_on_first_party_core` test name → rename that test method to `Should_keep_server_depending_only_on_first_party_core` and its `BeEquivalentTo(new[] { "SampleMcp.Core" })`, `ServiceShapeTests` namespace `SampleMcp.Server.Commands`, `CodeStructureTests` `srcDirs` entry `src/SampleMcp.Server` and `sourceAssemblyMap` key `"SampleMcp.Server"` → `".Server"`, `.slnx` project paths, `InternalsVisibleTo` in the host csproj, and the Integration csproj `ProjectReference` to the host.

- [ ] **Step 3: Update `template.json`**

Replace `dotnet/templates/mcp/.template.config/template.json` with:

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "ryan75195",
  "classifications": ["MCP", "Server", "Web"],
  "identity": "SampleMcp.Template",
  "name": "MCP server solution",
  "shortName": "mcp",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "SampleMcp",
  "preferNameDirectory": true,
  "primaryOutputs": [
    { "path": "src/SampleMcp.Server/SampleMcp.Server.csproj" }
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

Also confirm `harness-manifest.json` ownedPaths references `src/SampleMcp.Analyzers/**` (renamed from `ConsoleApp.Analyzers`), and rename the host `UserSecretsId` GUID line is fine as-is (any GUID works).

- [ ] **Step 4: Verify the renamed base scaffolds and passes green**

```bash
cd C:/Users/ryan7/programming/agent-project-templates
dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-t1"; dotnet new mcp -n RenameCheck -o "$(cygpath -w "$TEMP")/mcp-t1"
cd "$TEMP/mcp-t1" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: build succeeds; all tests pass. (At this point the host `SampleMcp.Server` is still a console Worker app — that's expected; Task 2 converts it.)

- [ ] **Step 5: Commit**

```bash
cd C:/Users/ryan7/programming/agent-project-templates
git add dotnet/templates/mcp
git commit -m "Add mcp template as a renamed copy of the cli template base"
```

---

### Task 2: Convert the host project to an ASP.NET Core MCP server

**Files:**
- Modify: `dotnet/templates/mcp/src/SampleMcp.Server/SampleMcp.Server.csproj`
- Modify: `dotnet/templates/mcp/src/SampleMcp.Server/Program.cs`

**Interfaces:** Consumes the renamed base from Task 1. Produces an ASP.NET Core MCP host (no primitives yet) exposing MCP over HTTP, with `public partial class Program;` for integration testing. Later tasks add the Core service, primitives, and protocol test.

- [ ] **Step 1: Rewrite the host csproj**

Replace `dotnet/templates/mcp/src/SampleMcp.Server/SampleMcp.Server.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>dotnet-SampleMcp.Server-6f1b2c3d-4e5a-6b7c-8d9e-0a1b2c3d4e5f</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol.AspNetCore" Version="2.0.0-preview.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SampleMcp.Core\SampleMcp.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>SampleMcp.Tests.Unit</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Rewrite Program.cs as the MCP host**

Replace `dotnet/templates/mcp/src/SampleMcp.Server/Program.cs` with:

```csharp
using SampleMcp.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

var app = builder.Build();
app.MapMcp();
app.Run();

public partial class Program;
```

- [ ] **Step 3: Verify green**

```bash
cd C:/Users/ryan7/programming/agent-project-templates && dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-t2"; dotnet new mcp -n HostCheck -o "$(cygpath -w "$TEMP")/mcp-t2"
cd "$TEMP/mcp-t2" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: green. The LayerDependency test `Should_keep_server_depending_only_on_first_party_core` passes because Server first-party-references only Core (MCP packages are third-party).

- [ ] **Step 4: Commit**

```bash
git add dotnet/templates/mcp/src/SampleMcp.Server
git commit -m "Convert mcp template host to an ASP.NET Core MCP server"
```

---

### Task 3: Add the Core service (interface + impl + DI + unit fixture)

**Files:**
- Create: `dotnet/templates/mcp/src/SampleMcp.Core/Interfaces/IGreetingService.cs`
- Create: `dotnet/templates/mcp/src/SampleMcp.Core/Services/GreetingService.cs`
- Modify: `dotnet/templates/mcp/src/SampleMcp.Core/ServiceCollectionExtensions.cs`
- Modify: `dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/TestHelpers.cs`
- Create: `dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Core/GreetingServiceTests.cs`

**Interfaces:** Produces `SampleMcp.Core.Interfaces.IGreetingService` (method `string Greet(string name)`) and `SampleMcp.Core.Services.GreetingService`, registered in `AddCoreServices`. Consumed by the tool in Task 4.

- [ ] **Step 1: Create the interface**

`dotnet/templates/mcp/src/SampleMcp.Core/Interfaces/IGreetingService.cs`:

```csharp
namespace SampleMcp.Core.Interfaces;

public interface IGreetingService
{
    string Greet(string name);
}
```

- [ ] **Step 2: Create the implementation**

`dotnet/templates/mcp/src/SampleMcp.Core/Services/GreetingService.cs`:

```csharp
using SampleMcp.Core.Interfaces;

namespace SampleMcp.Core.Services;

public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
```

- [ ] **Step 3: Register it in AddCoreServices**

Replace `dotnet/templates/mcp/src/SampleMcp.Core/ServiceCollectionExtensions.cs` with:

```csharp
using SampleMcp.Core.Interfaces;
using SampleMcp.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SampleMcp.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
        return services;
    }
}
```

- [ ] **Step 4: Point ServiceNamespaces at the Core services namespace**

In `dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/TestHelpers.cs`, change the `ServiceNamespaces` array so `ServiceShapeTests`/`NamingConventionTests` check the new service:

```csharp
    public static readonly string[] ServiceNamespaces =
    [
        "SampleMcp.Core.Services"
    ];
```

- [ ] **Step 5: Add the unit fixture (in a `.Core` sub-namespace)**

`dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Core/GreetingServiceTests.cs`:

```csharp
using FluentAssertions;
using SampleMcp.Core.Services;

namespace SampleMcp.Tests.Unit.Core;

[TestFixture]
public class GreetingServiceTests
{
    [Test]
    public void Should_greet_the_given_name()
    {
        var service = new GreetingService();

        var result = service.Greet("Ada");

        result.Should().Be("Hello, Ada!");
    }
}
```

Note: the Unit test project must reference FluentAssertions. The cli Unit csproj already pulls the analyzer/test stack; if FluentAssertions is not referenced there, add `<PackageReference Include="FluentAssertions" Version="6.12.0" />` (match the version used in the Architecture project — check `dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/SampleMcp.Tests.Architecture.csproj`) to `SampleMcp.Tests.Unit.csproj`.

- [ ] **Step 6: Verify green**

```bash
cd C:/Users/ryan7/programming/agent-project-templates && dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-t3"; dotnet new mcp -n CoreCheck -o "$(cygpath -w "$TEMP")/mcp-t3"
cd "$TEMP/mcp-t3" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: green. DiRegistration finds `IGreetingService` registered; ServiceShape sees `GreetingService` implements an interface; CodeStructure finds `GreetingServiceTests` in a `.Core` namespace; CI0002 sees `Greet` covered.

- [ ] **Step 7: Commit**

```bash
git add dotnet/templates/mcp/src/SampleMcp.Core dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/TestHelpers.cs dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Core
git commit -m "Add Core greeting service to mcp template with DI and unit coverage"
```

---

### Task 4: Add the MCP primitives (tool, resource, prompt) + unit fixtures + suffix rule

**Files:**
- Create: `dotnet/templates/mcp/src/SampleMcp.Server/Tools/EchoTool.cs`
- Create: `dotnet/templates/mcp/src/SampleMcp.Server/Resources/GreetingResource.cs`
- Create: `dotnet/templates/mcp/src/SampleMcp.Server/Prompts/SummarizePrompt.cs`
- Modify: `dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/NamingConventionTests.cs`
- Create: `dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/EchoToolTests.cs`
- Create: `dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/GreetingResourceTests.cs`
- Create: `dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/SummarizePromptTests.cs`

**Interfaces:** Consumes `IGreetingService`. Produces three attribute-discovered MCP primitives named `echo` (tool), `greeting` (resource), `summarize` (prompt), covered by unit fixtures. Consumed by the protocol test in Task 5.

- [ ] **Step 1: Add the tool**

`dotnet/templates/mcp/src/SampleMcp.Server/Tools/EchoTool.cs`:

```csharp
using System.ComponentModel;
using SampleMcp.Core.Interfaces;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Tools;

[McpServerToolType]
public sealed class EchoTool(IGreetingService greeting)
{
    [McpServerTool(Name = "echo"), Description("Greets the supplied name via the Core greeting service.")]
    public string Echo([Description("The name to greet.")] string name) => greeting.Greet(name);
}
```

- [ ] **Step 2: Add the resource**

`dotnet/templates/mcp/src/SampleMcp.Server/Resources/GreetingResource.cs`:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Resources;

[McpServerResourceType]
public sealed class GreetingResource
{
    [McpServerResource(UriTemplate = "mcp://greeting", Name = "greeting", MimeType = "text/plain")]
    [Description("A sample greeting resource served by this MCP server.")]
    public string Read() => "Hello from the sample MCP resource.";
}
```

- [ ] **Step 3: Add the prompt**

`dotnet/templates/mcp/src/SampleMcp.Server/Prompts/SummarizePrompt.cs`:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcp.Server.Prompts;

[McpServerPromptType]
public sealed class SummarizePrompt
{
    [McpServerPrompt(Name = "summarize"), Description("Builds a prompt asking the model to summarize the given text.")]
    public string Build([Description("The text to summarize.")] string text) => $"Please summarize the following text:\n\n{text}";
}
```

- [ ] **Step 4: Allow the Tool/Resource/Prompt role suffixes**

In `dotnet/templates/mcp/tests/SampleMcp.Tests.Architecture/NamingConventionTests.cs`, extend `AllowedSuffixes` to include the MCP roles:

```csharp
    private static readonly HashSet<string> AllowedSuffixes =
    [
        "Service", "Repository", "Client", "Store", "Context",
        "Entity", "Command", "Parser", "Converter", "Pool",
        "Worker", "Process", "Extensions", "Mapper", "Extractor",
        "Probe", "Result", "Monitor", "Plugin", "Filter",
        "Tool", "Resource", "Prompt"
    ];
```

- [ ] **Step 5: Add unit fixtures for each primitive (in a `.Server` sub-namespace)**

`dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/EchoToolTests.cs`:

```csharp
using FluentAssertions;
using SampleMcp.Core.Services;
using SampleMcp.Server.Tools;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class EchoToolTests
{
    [Test]
    public void Should_echo_a_greeting_for_the_name()
    {
        var tool = new EchoTool(new GreetingService());

        var result = tool.Echo("Ada");

        result.Should().Be("Hello, Ada!");
    }
}
```

`dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/GreetingResourceTests.cs`:

```csharp
using FluentAssertions;
using SampleMcp.Server.Resources;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class GreetingResourceTests
{
    [Test]
    public void Should_return_the_sample_greeting_text()
    {
        var resource = new GreetingResource();

        var result = resource.Read();

        result.Should().Contain("sample MCP resource");
    }
}
```

`dotnet/templates/mcp/tests/SampleMcp.Tests.Unit/Server/SummarizePromptTests.cs`:

```csharp
using FluentAssertions;
using SampleMcp.Server.Prompts;

namespace SampleMcp.Tests.Unit.Server;

[TestFixture]
public class SummarizePromptTests
{
    [Test]
    public void Should_build_a_summarize_prompt_containing_the_text()
    {
        var prompt = new SummarizePrompt();

        var result = prompt.Build("hello world");

        result.Should().Contain("hello world");
    }
}
```

- [ ] **Step 6: Verify green**

```bash
cd C:/Users/ryan7/programming/agent-project-templates && dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-t4"; dotnet new mcp -n PrimitivesCheck -o "$(cygpath -w "$TEMP")/mcp-t4"
cd "$TEMP/mcp-t4" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: green. NamingConvention accepts `EchoTool`/`GreetingResource`/`SummarizePrompt`; CodeStructure finds the three `*Tests` fixtures in `.Server` namespaces; CI0002 sees `Echo`/`Read`/`Build` covered; ConstructorDependency/ServiceShape accept the `IGreetingService` ctor param (an interface).

- [ ] **Step 7: Commit**

```bash
git add dotnet/templates/mcp/src/SampleMcp.Server dotnet/templates/mcp/tests
git commit -m "Add sample MCP tool, resource, and prompt with unit coverage"
```

---

### Task 5: Add the MCP protocol integration test

**Files:**
- Modify: `dotnet/templates/mcp/tests/SampleMcp.Tests.Integration/SampleMcp.Tests.Integration.csproj`
- Delete: `dotnet/templates/mcp/tests/SampleMcp.Tests.Integration/SmokeTests.cs`
- Create: `dotnet/templates/mcp/tests/SampleMcp.Tests.Integration/ProtocolTests.cs`

**Interfaces:** Consumes the running server (via `WebApplicationFactory<Program>`) and the MCP client SDK. Produces a real protocol test that handshakes, lists all three primitives, and calls the tool. (Fixture named `ProtocolTests` — source type `McpServerProtocol` does not exist, so CI0002 imposes no coverage requirement.)

- [ ] **Step 1: Add the client + test-host packages to the Integration csproj**

In `dotnet/templates/mcp/tests/SampleMcp.Tests.Integration/SampleMcp.Tests.Integration.csproj`, add to the package `ItemGroup`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9" />
    <PackageReference Include="ModelContextProtocol" Version="2.0.0-preview.2" />
```

Confirm the project already has a `ProjectReference` to `..\..\src\SampleMcp.Server\SampleMcp.Server.csproj` (renamed from the cli host in Task 1); if not, add it. Leave the existing NUnit/coverlet references as-is.

- [ ] **Step 2: Replace the smoke test with the protocol test**

Delete `SmokeTests.cs`. Create `dotnet/templates/mcp/tests/SampleMcp.Tests.Integration/ProtocolTests.cs`:

```csharp
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace SampleMcp.Tests.Integration;

[TestFixture]
public class ProtocolTests
{
    private static async Task<McpClient> ConnectClient(WebApplicationFactory<Program> factory)
    {
        HttpClient http = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = http.BaseAddress! },
            http, NullLoggerFactory.Instance, false);
        return await McpClient.CreateAsync(transport);
    }

    [Test]
    public async Task Should_list_the_sample_tool_resource_and_prompt()
    {
        using var factory = new WebApplicationFactory<Program>();
        await using var client = await ConnectClient(factory);

        var tools = await client.ListToolsAsync();
        var resources = await client.ListResourcesAsync();
        var prompts = await client.ListPromptsAsync();

        tools.Select(t => t.Name).Should().Contain("echo");
        resources.Should().NotBeEmpty();
        prompts.Should().NotBeEmpty();
    }

    [Test]
    public async Task Should_call_the_echo_tool_over_the_protocol()
    {
        using var factory = new WebApplicationFactory<Program>();
        await using var client = await ConnectClient(factory);

        var result = await client.CallToolAsync(
            "echo", new Dictionary<string, object?> { ["name"] = "Ada" });

        result.Should().NotBeNull();
    }
}
```

Note: `WebApplicationFactory<Program>` binds to the `public partial class Program` added in Task 2. The Integration project must reference FluentAssertions (add the same version as the Architecture project if not already present). If the compiler cannot find `Program`, ensure the Integration csproj references the Server project and that the Server's `Program.cs` ends with `public partial class Program;`.

- [ ] **Step 3: Verify green (this actually runs the MCP server in-memory)**

```bash
cd C:/Users/ryan7/programming/agent-project-templates && dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-t5"; dotnet new mcp -n ProtocolCheck -o "$(cygpath -w "$TEMP")/mcp-t5"
cd "$TEMP/mcp-t5" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: green, including the two `ProtocolTests` — the client connects over the in-memory handler, handshakes, lists all three primitives, and calls `echo`. (Verified working in a standalone spike before this plan.)

- [ ] **Step 4: Commit**

```bash
git add dotnet/templates/mcp/tests/SampleMcp.Tests.Integration
git commit -m "Add MCP protocol integration test to the mcp template"
```

---

### Task 6: Wire the mcp template into the ecosystem

**Files:**
- Modify: `plugin/scripts/write-stamp.ps1`
- Create: `new-project/handlers/dotnet-mcp.ps1`
- Modify: `new-project/tests/lib-tests.ps1`
- Create: `plugin/skills/new-dotnet-mcp/SKILL.md`
- Modify: `plugin/template-tests/validate-plugin.ps1`
- Modify: `.github/workflows/template-ci.yml`
- Modify: `dotnet/README.md`

**Interfaces:** Makes `dotnet-mcp` a first-class `new-project` type, stamped as `mcp`, discoverable by the skill and CI.

- [ ] **Step 1: Add `mcp` to the provenance stamp**

In `plugin/scripts/write-stamp.ps1`, add `'mcp'` to the `[ValidateSet(...)]` on `$Template` (making it `'cli', 'etl-api', 'expo-app', 'mcp'`) and add to the `$templateDirs` hashtable:

```powershell
    'mcp'      = 'dotnet/templates/mcp'
```

- [ ] **Step 2: Add the CLI handler**

Create `new-project/handlers/dotnet-mcp.ps1`:

```powershell
@{
    Type        = 'dotnet-mcp'
    Description = '.NET 10 MCP server (HTTP/streamable, ModelContextProtocol SDK, analyzers + architecture tests)'
    StampName   = 'mcp'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new mcp -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new mcp failed (exit $LASTEXITCODE)" }
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

- [ ] **Step 3: Update the discovery assertion in lib-tests**

In `new-project/tests/lib-tests.ps1`, change the discovery assertion to include the new type:

```powershell
Assert (($types -join ',') -eq 'dotnet-cli,dotnet-etl-api,dotnet-mcp,expo') "discovery finds four types, got $($types -join ',')"
```

- [ ] **Step 4: Add the plugin skill**

Create `plugin/skills/new-dotnet-mcp/SKILL.md`:

```markdown
---
name: new-dotnet-mcp
description: Use when creating a new .NET MCP server from the agent-harness template ("new mcp server", "new model context protocol server", "new dotnet mcp"). Scaffolds via the new-project CLI, which stamps provenance and verifies guardrails.
---

# New .NET MCP Server Project

Scaffold a .NET 10 MCP server (HTTP/streamable, ModelContextProtocol SDK) by delegating
to the standalone `new-project` CLI (the single source of truth for scaffold -> stamp ->
setup -> verify).

## Inputs

- **Name** (required): PascalCase solution name, e.g. `WeatherMcp`. Ask if not given.
- **Destination** (optional): defaults to `<cwd>/<Name>`.

## Steps

1. **Resolve the harness repo:**
   ```powershell
   $repo = & "${CLAUDE_PLUGIN_ROOT}/scripts/resolve-repo.ps1" | Select-Object -Last 1
   ```
2. **Run the CLI** (omit `-Destination` when defaulted):
   ```powershell
   & "$repo/new-project.ps1" dotnet-mcp <Name> -Destination <dir>
   ```
   This installs the templates, scaffolds, stamps provenance, runs setup, and verifies
   (`dotnet build` + `dotnet test`, including the MCP protocol integration test). If it
   prints a git-identity error, relay the fix and stop until the user configures git,
   then re-run.
3. **Report** what the CLI printed: the project path, that the server speaks MCP over
   HTTP with a sample tool/resource/prompt, and the development lifecycle from the
   project's CLAUDE.md (issue -> feat branch -> commit -> PR).

Never hand-edit `.harness.json` — the CLI writes it.
```

- [ ] **Step 5: Require the new skill in plugin validation**

In `plugin/template-tests/validate-plugin.ps1`, add `'new-dotnet-mcp'` to the `$required` array inside the `-RequireFull` block.

- [ ] **Step 6: Add `mcp` to the CI scaffold matrix**

In `.github/workflows/template-ci.yml`, change the `scaffold-and-test` job matrix:

```yaml
        template: [cli, etl-api, mcp]
```

- [ ] **Step 7: Document the template in the dotnet README**

In `dotnet/README.md`, under "The two templates" table (update the heading/count as appropriate — it becomes three), add a row for the `mcp` short name describing the MCP server shape and when to pick it.

- [ ] **Step 7b: Rewrite the template's own README and check CLAUDE.md**

The `dotnet/templates/mcp/README.md` was copied from the cli template and still describes a console CLI. Rewrite it to describe the MCP server: that it serves MCP over HTTP/streamable transport (`app.MapMcp()`), ships a sample tool (`echo`), resource (`greeting`), and prompt (`summarize`), how to run it (`dotnet run --project src/SampleMcp.Server`) and point an MCP client at it, and that it is **unauthenticated by default** with a note that authentication (e.g. OAuth) can be added at the ASP.NET Core layer as an extension point. Then skim `dotnet/templates/mcp/CLAUDE.md`: keep the generic lifecycle/code-style sections, and fix any wording that is specific to the CLI/console shape (e.g. "how to run" references) so it matches the MCP server.

- [ ] **Step 8: Verify the wiring**

```bash
cd C:/Users/ryan7/programming/agent-project-templates
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/lib-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File plugin/template-tests/validate-plugin.ps1 -RequireFull
powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1 dotnet-mcp Demo -DryRun
```
Expected: `lib-tests: all passed`; `Plugin validation passed.`; a dry-run plan for `dotnet-mcp` listing pre-install + scaffold + stamp + setup + verify.

- [ ] **Step 9: Commit**

```bash
git add plugin/scripts/write-stamp.ps1 new-project/handlers/dotnet-mcp.ps1 new-project/tests/lib-tests.ps1 "plugin/skills/new-dotnet-mcp/SKILL.md" plugin/template-tests/validate-plugin.ps1 .github/workflows/template-ci.yml dotnet/README.md dotnet/templates/mcp/README.md dotnet/templates/mcp/CLAUDE.md
git commit -m "Wire the mcp template into new-project, skills, stamp, and CI"
```

---

### Task 7: Full end-to-end verification

**Files:** none (validation only).

- [ ] **Step 1: Scaffold and test the template directly**

```bash
cd C:/Users/ryan7/programming/agent-project-templates && dotnet new install ./dotnet --force
rm -rf "$TEMP/mcp-final"; dotnet new mcp -n FinalMcp -o "$(cygpath -w "$TEMP")/mcp-final"
cd "$TEMP/mcp-final" && dotnet build --no-incremental && dotnet test --no-build --verbosity minimal
```
Expected: build green; all tests pass (analyzers, architecture, unit, and the MCP protocol integration test).

- [ ] **Step 2: Scaffold through the `new-project` CLI end-to-end**

Requires a configured git identity (present in this environment).

```bash
cd C:/Users/ryan7/programming/agent-project-templates
rm -rf "$TEMP/mcp-cli"; powershell -NoProfile -ExecutionPolicy Bypass -File new-project.ps1 dotnet-mcp CliMcp -Destination "$(cygpath -w "$TEMP")/mcp-cli"
echo "exit=$?"
# assert stamp
cat "$TEMP/mcp-cli/.harness.json" | grep -q '"template": *"mcp"' && echo "stamp ok"
```
Expected: exit 0, "Created dotnet-mcp project ..." report, and `.harness.json` with `template: mcp`.

- [ ] **Step 3: Run the ecosystem test suites**

```bash
cd C:/Users/ryan7/programming/agent-project-templates
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/lib-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File new-project/tests/dispatcher-args-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File plugin/template-tests/validate-plugin.ps1 -RequireFull
```
Expected: all pass.

- [ ] **Step 4: Commit (only if Steps 1–3 required a fix)**

If a fix was needed, commit it; otherwise this task is pure verification.

```bash
git add -A
git commit -m "Fix issues surfaced by mcp template end-to-end verification"
```
