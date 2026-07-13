# .NET MCP server template — design

Date: 2026-07-13
Status: Approved (design), pending implementation plan

## Problem

`agent-project-templates` ships `dotnet new` templates for a CLI and an ETL+API
service, each wired into the `new-project` CLI, the plugin skills, provenance
stamping, and CI. There is no template for a **.NET MCP (Model Context Protocol)
server**. This adds one — a `dotnet new mcp` template following the same house style
(guardrails, analyzers, architecture tests, DI/Core layering, agent configs) — and
wires it into the ecosystem as a first-class type like the others.

## Goals

- A `dotnet new mcp` template producing a .NET 10 MCP server solution that builds,
  tests, and passes the full guardrail suite out of the box.
- HTTP / streamable transport via the official `ModelContextProtocol` C# SDK.
- Demonstrate all three MCP primitives (a tool, a resource, a prompt), with the tool
  showing the DI → Core layering.
- A real protocol-level integration test (client connects, handshakes, lists, calls).
- Full ecosystem integration: provenance stamp, `new-project` CLI handler, plugin
  skill, CI, plugin validation.

## Non-goals

- **No stdio transport** (HTTP/streamable only).
- **No authentication** in the scaffold (unauthenticated local/dev default with a
  documented extension point for OAuth; auth is deployment-specific).
- No additional MCP primitives beyond one of each (tool/resource/prompt).
- No changes to the existing cli/etl-api templates beyond shared-skeleton parity.

## Design decisions (from brainstorming)

- **Transport:** HTTP / streamable (ASP.NET Core host), not stdio.
- **Sample surface:** one tool + one resource + one prompt.
- **Auth:** none by default; documented extension point.
- **Test depth:** real MCP protocol integration test (the MCP surface is exercised
  over the wire, not by instantiating classes).

## Architecture

New template directory `dotnet/templates/mcp`, a .NET 10 solution mirroring
`dotnet/templates/etl-api`, swapping the web API for an MCP host.

### Project layout

```
dotnet/templates/mcp/
  .template.config/template.json     (shortName mcp, sourceName McpServer)
  McpServer.slnx
  Directory.Build.props
  setup.ps1, README.md, CLAUDE.md, harness-manifest.json
  .editorconfig, .gitignore
  .claude/ .codex/ .githooks/         (agent configs + guardrail hooks)
  src/
    McpServer.Server/                 ASP.NET Core MCP host
      Program.cs
      Tools/        (one [McpServerToolType])
      Resources/    (one [McpServerResourceType])
      Prompts/      (one [McpServerPromptType])
      AssemblyMarker.cs
    McpServer.Core/                   business logic the tool delegates to
      ServiceCollectionExtensions.cs
      AssemblyMarker.cs
      (a sample service, e.g. a small pure-logic service)
    McpServer.Analyzers/              the shared 16-analyzer Roslyn suite (copied)
  tests/
    McpServer.Tests.Unit/             Core service logic
    McpServer.Tests.Integration/      real MCP protocol test (WebApplicationFactory + MCP client)
    McpServer.Tests.Architecture/     layer/DI/naming rules
    McpServer.Tests.Analyzers/        analyzer tests (copied)
```

### Copied vs. new

**Copied from etl-api (identical guardrail skeleton, with `EtlApi`→`McpServer`
renames):** the `Analyzers` project and its tests, the `Architecture` test harness,
`.claude`/`.codex` agent configs, `.githooks`, `.editorconfig`, `Directory.Build.props`,
`setup.ps1`, `harness-manifest.json`, `.gitignore`.

**New / unique:** the `Server` project (Program.cs + Tools/Resources/Prompts), the
`Core` service, the `Tests.Integration` MCP protocol test, `template.json`, the
`.slnx`, and README/CLAUDE.md content.

### MCP host (`McpServer.Server`)

- ASP.NET Core `WebApplication`. MCP registered via the SDK:
  `builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly()
  .WithResourcesFromAssembly().WithPromptsFromAssembly();` and `app.MapMcp();`.
  Package `ModelContextProtocol.AspNetCore` (0.x preview — exact version pinned during
  implementation against the restored package).
- **Sample primitives**, attribute-discovered:
  - A `[McpServerToolType]` class with a `[McpServerTool]` method that takes a
    **constructor-injected `Core` service** (demonstrates DI → Core layering, and is
    consistent with the ConstructorDependency / ServiceShape analyzers).
  - A `[McpServerResourceType]` class with a `[McpServerResource]`.
  - A `[McpServerPromptType]` class with a `[McpServerPrompt]`.
- **Unauthenticated** by default; a clearly-marked extension point + README note for
  adding OAuth later.
- `public partial class Program {}` exposed at the end of `Program.cs` so the
  integration test's `WebApplicationFactory<Program>` can host it.

### Layering

- Architecture tests enforce `Server → Core` and `Core → nothing` (same shape as
  etl-api's `Api → Core`, renamed). Tools/resources/prompts live in `Server` and call
  into `Core`.

## Testing

- **`McpServer.Tests.Integration` (protocol-level, the headline test):**
  `WebApplicationFactory<Program>` hosts the server in-memory; the MCP **client SDK**
  connects over the streamable-HTTP transport (wired to the factory's test handler),
  performs the `initialize` handshake, lists tools/resources/prompts (asserting the
  three samples are present), and calls the sample tool (asserting the real MCP
  response).
  - **Known risk / fallback:** wiring the SDK's streamable-HTTP *client* transport to a
    `WebApplicationFactory` in-memory `HttpMessageHandler` (vs a real socket) is the one
    novel integration detail, and the SDK is preview. If the in-memory handler cannot
    carry the transport, fall back to hosting on a loopback Kestrel port for the test.
- **`McpServer.Tests.Unit`:** the `Core` service (pure logic) is unit-tested directly.
  The thin tool/resource/prompt classes are covered by the integration test.
- **`Architecture` + `Analyzers` tests:** carried over from the skeleton.
- **Guardrail compatibility:** ensure the `TestCoverageAnalyzer` (and the other
  analyzers/architecture rules) are satisfied by this unit-for-Core / integration-for-
  surface split rather than fought; adjust test placement or add minimal direct tests
  if a guardrail requires coverage the integration test doesn't credit.

## Ecosystem integration

- **`plugin/scripts/write-stamp.ps1`:** add `mcp` to the `-Template` `ValidateSet` and
  add `'mcp' = 'dotnet/templates/mcp'` to the `templateDirs` map.
- **`new-project/handlers/dotnet-mcp.ps1`:** new handler — Type `dotnet-mcp`, StampName
  `mcp`, no ExtraArgs, PreInstall `dotnet new install "$repo/dotnet" --force`, Scaffold
  `dotnet new mcp -n <Name> -o <Dest>`, Verify `dotnet build` + `dotnet test` (mirrors
  the dotnet-cli/etl-api handlers).
- **`new-project/tests/lib-tests.ps1`:** update the discovery assertion from
  `dotnet-cli,dotnet-etl-api,expo` to `dotnet-cli,dotnet-etl-api,dotnet-mcp,expo`.
- **`plugin/skills/new-dotnet-mcp/SKILL.md`:** new thin delegate skill (matches the
  other three; delegates to `new-project.ps1 dotnet-mcp`).
- **`plugin/template-tests/validate-plugin.ps1`:** add `new-dotnet-mcp` to the
  `-RequireFull` required-skills list.
- **CI (`.github/workflows/template-ci.yml`):** add `mcp` to the `scaffold-and-test`
  matrix (`[cli, etl-api]` → `[cli, etl-api, mcp]`), so the scaffolded MCP server is
  built and its tests (including the protocol integration test) run in CI.
- **`dotnet/README.md`:** mention the new template.

## Success criteria

- `dotnet new install ./dotnet` then `dotnet new mcp -n Foo` produces a solution that
  `dotnet build` and `dotnet test` pass, with all analyzers/architecture rules green.
- The integration test proves a client can connect over HTTP, handshake, list the three
  sample primitives, and call the tool.
- `new-project dotnet-mcp Foo` scaffolds, stamps (`template=mcp`), sets up, and verifies
  the project end-to-end.
- The `new-dotnet-mcp` skill, CI matrix entry, and plugin validation all recognize the
  new type.
