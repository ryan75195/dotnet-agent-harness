# SampleMcp

MCP server solution scaffolded from the [dotnet-agent-harness](https://github.com/ryan75195/dotnet-agent-harness) `mcp` template.

## What this is

An ASP.NET Core host that speaks the [Model Context Protocol](https://modelcontextprotocol.io) over
HTTP using the streamable-HTTP transport (`app.MapMcp()`), built on the official
`ModelContextProtocol` SDK. It ships with one of each primitive to fork from:

- **Tool** — `echo` (`src/SampleMcp.Server/Tools/EchoTool.cs`): greets a supplied name via a `Core`
  service, demonstrating how tools stay thin and delegate business logic to `Core`.
- **Resource** — `greeting` (`src/SampleMcp.Server/Resources/GreetingResource.cs`): served at
  `mcp://greeting`, returns a static text payload.
- **Prompt** — `summarize` (`src/SampleMcp.Server/Prompts/SummarizePrompt.cs`): builds a
  summarization prompt from supplied text.

Tools, resources, and prompts are discovered from the assembly automatically
(`WithToolsFromAssembly()`, `WithResourcesFromAssembly()`, `WithPromptsFromAssembly()` in
`Program.cs`) — add a new `[McpServerToolType]` (or resource/prompt type) class under `src/SampleMcp.Server/`
and it's picked up with no registration step.

## First-time setup

After scaffolding (`dotnet new mcp -n SampleMcp`), run once:

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
dotnet run --project src/SampleMcp.Server
```

By default this serves MCP over HTTP/streamable transport on the ASP.NET Core Kestrel endpoint
printed to the console at startup (the default `http://localhost:5000`-style binding unless you
configure `ASPNETCORE_URLS` or add `launchSettings.json`). Point any MCP HTTP client at that base
URL to connect — for example, an editor or agent that supports adding a remote MCP server by URL.

## Authentication

The scaffolded server ships with **optional OAuth, off by default**. It stays unauthenticated —
anyone who can reach the HTTP endpoint can call its tools — until you set `McpAuth:Authority` in
configuration. Then `Program.cs` protects the MCP endpoint and serves spec-compliant OAuth
discovery, so MCP clients (editors, agents) can connect via the standard authorization flow.

`AddMcpOAuth(configuration)` in `Authentication/McpOAuthExtensions.cs` wires the ASP.NET Core auth
layer using the MCP SDK's `.AddMcp()`:

- **JWT bearer** validation against your OIDC provider (`Authority` + `Audience`).
- **`.AddMcp()`** serves OAuth Protected Resource Metadata at
  `/.well-known/oauth-protected-resource` plus the `WWW-Authenticate` challenge, so clients can
  discover the authorization server and self-register (DCR) — the seamless connect flow the
  [MCP authorization spec](https://modelcontextprotocol.io/specification/draft/basic/authorization)
  describes.
- `MapMcp().RequireAuthorization()` is applied only when auth is enabled.

Enable it via configuration (appsettings.json, env vars, or user-secrets):

```json
{
  "McpAuth": {
    "Authority": "https://your-idp.example.com/",
    "Audience": "api://your-api-or-mcp-identifier",
    "Resource": "api://your-api-or-mcp-identifier"
  }
}
```

`Resource` is the audience the client requests a token for (defaults to `Audience`). If the token
is meant for a downstream API you call from your tools, set these to that API's audience and
forward the bearer to it.

`Program.cs` also enables `UseForwardedHeaders()` so OAuth discovery URLs are correct (`https`,
public host) when the server runs behind a TLS-terminating reverse proxy or tunnel.

> **Note:** In the C# SDK the MCP server is an OAuth *resource server* — Dynamic Client
> Registration happens at your **authorization server**, not here. Some providers (e.g. Auth0)
> require enabling DCR and RFC 8707 resource-parameter support on the tenant for the fully
> seamless, no-manual-client-id flow.

## Development lifecycle

See [CLAUDE.md](./CLAUDE.md) for the full lifecycle (issue → branch → commit → PR).

Quick summary:
1. `gh issue create --title "..."` (every change starts with an issue)
2. `git checkout -b feat/<issue-num>-<slug>` (`reference-transaction` hook verifies the issue exists)
3. Edit + commit (pre-commit hook runs build, format, tests)
4. `gh pr create` and squash-merge

Direct commits to `main` are blocked. Edits to already-merged branches are blocked.
