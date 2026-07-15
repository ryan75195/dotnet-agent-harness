# SampleDurable

MCP server solution scaffolded from the [dotnet-agent-harness](https://github.com/ryan75195/dotnet-agent-harness) `mcp` template.

## What this is

An ASP.NET Core host that speaks the [Model Context Protocol](https://modelcontextprotocol.io) over
HTTP using the streamable-HTTP transport (`app.MapMcp()`), built on the official
`ModelContextProtocol` SDK. It ships with one of each primitive to fork from:

- **Tool** â€” `echo` (`src/SampleDurable.Server/Tools/EchoTool.cs`): greets a supplied name via a `Core`
  service, demonstrating how tools stay thin and delegate business logic to `Core`.
- **Resource** â€” `greeting` (`src/SampleDurable.Server/Resources/GreetingResource.cs`): served at
  `mcp://greeting`, returns a static text payload.
- **Prompt** â€” `summarize` (`src/SampleDurable.Server/Prompts/SummarizePrompt.cs`): builds a
  summarization prompt from supplied text.

Tools, resources, and prompts are discovered from the assembly automatically
(`WithToolsFromAssembly()`, `WithResourcesFromAssembly()`, `WithPromptsFromAssembly()` in
`Program.cs`) â€” add a new `[McpServerToolType]` (or resource/prompt type) class under `src/SampleDurable.Server/`
and it's picked up with no registration step.

## First-time setup

After scaffolding (`dotnet new mcp -n SampleDurable`), run once:

```powershell
.\setup.ps1
```

This initializes a git repo, activates `.githooks/` for the project lifecycle, and creates the initial commit.

## Build and test

```powershell
dotnet restore
dotnet build
dotnet test
```

## Run the server

```powershell
dotnet run --project src/SampleDurable.Server
```

By default this serves MCP over HTTP/streamable transport on the ASP.NET Core Kestrel endpoint
printed to the console at startup (the default `http://localhost:5000`-style binding unless you
configure `ASPNETCORE_URLS` or add `launchSettings.json`). Point any MCP HTTP client at that base
URL to connect â€” for example, an editor or agent that supports adding a remote MCP server by URL.

## Authentication

The scaffolded server is **unauthenticated by default** â€” anyone who can reach the HTTP endpoint
can call its tools, resources, and prompts. This is intentional for local development and as a
starting point. Before exposing the server beyond localhost, add authentication and authorization
at the ASP.NET Core layer (the same layer any minimal-API or controller-based app uses): wire up
`AddAuthentication()`/`AddAuthorization()` (e.g. an OAuth2/OIDC bearer scheme), apply
`RequireAuthorization()` to the MCP endpoint mapped by `app.MapMcp()`, and configure your identity
provider of choice. The MCP specification's [authorization spec](https://modelcontextprotocol.io/specification/draft/basic/authorization)
describes the OAuth flow MCP clients expect if you want spec-compliant auth discovery.

## Development lifecycle

See [CLAUDE.md](./CLAUDE.md) for the full lifecycle (issue â†’ branch â†’ commit â†’ PR).

Quick summary:
1. `gh issue create --title "..."` (every change starts with an issue)
2. `git checkout -b feat/<issue-num>-<slug>` (`reference-transaction` hook verifies the issue exists)
3. Edit + commit (pre-commit hook runs build, format, tests)
4. `gh pr create` and squash-merge

Direct commits to `main` are blocked. Edits to already-merged branches are blocked.
