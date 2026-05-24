# CLAUDE.md

Project context for Claude Code sessions. Read this before making changes.

## Development lifecycle

Every change follows this loop. None of these steps are optional — hooks enforce each transition.

1. **Open an issue.** `gh issue create --title "..."`. No issue, no branch.
2. **Create a feat branch.** `git checkout -b feat/<N>-<kebab-slug>` where `<N>` is the issue number. `.githooks/reference-transaction` rejects the branch on creation if the name doesn't match or if issue #N doesn't exist on GitHub.
3. **Edit + test.** Normal development. Analyzers run on every build.
4. **Commit.** `git commit`. `.githooks/pre-commit` runs:
   - Branch guard (no commits to `main`/`master`)
   - Merged-branch check (`.claude/hooks/block-merged-branch.sh`)
   - `dotnet build --no-incremental` — all analyzers at error severity block
   - `dotnet format --verify-no-changes` — style, encoding, line endings
   - `dotnet test` — Unit + Architecture projects (Integration not yet wired in)
5. **Open PR.** `gh pr create --base main --head feat/<N>-<slug>`.
6. **Squash merge.** `gh pr merge <N> --squash --delete-branch`.

**Direct edits and commits to `main` are blocked.** **Edits to an already-merged branch are blocked** (Claude Code `PreToolUse` hook + pre-commit).

## Code style

- **No comments.** `NoCommentsAnalyzer` (CI0013) blocks `//`, `/* */`, and `///` at error severity. Extract intent into method names, variable names, or types. If a WHY is genuinely non-obvious (hidden constraint, bug workaround), extract it into a named helper — don't write a comment.
- **`TreatWarningsAsErrors=true`** with `AnalysisLevel=latest-all`. Any CA/IDE/CS/CI diagnostic at severity `error` breaks the build. Exception: NU1510 shows as a non-blocking warning.
- Full style rules live in `.editorconfig` (root + `tests/` override). Nullable reference types enabled everywhere. File-scoped namespaces, Allman braces, `_camelCase` private fields, `I`-prefixed interfaces.

## Architecture

- **Solution:** `ConsoleApp.slnx`, .NET 10, three `src/` projects (`Core`, `Cli`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
- **Architecture tests** in `tests/ConsoleApp.Tests.Architecture/` enforce layering, DI shape, DI wiring (every public Core interface must be registered via `Core.ServiceCollectionExtensions.AddCoreServices()`), naming conventions, and one-public-type-per-file. Tests are split across `LayerDependencyTests`, `NamingConventionTests`, `ServiceShapeTests`, `CodeStructureTests`, and `DiRegistrationTests`; shared infrastructure lives in `TestHelpers.cs`.
- **Custom analyzers** in `src/ConsoleApp.Analyzers/` enforce CI0001-CI0013 (method length, ctor param count, no tuple returns, no anonymous serialization, no comments, etc).

## Testing judgment calls

The toolchain enforces coverage (CI0002), assertion quality (CI0009), fixture existence, and naming. What it cannot enforce is *what kind* of test to write and *what* to assert on.

**Unit vs integration:** a unit test mocks boundaries; an integration test crosses them. Decide based on: *if the mock were wrong, would you ship a bug?* Entry points (endpoints, background services) and persistence (database writes) almost always need integration tests — a mock can't verify routing, schema, serialization wire format, or transaction behavior. Everything else: unit test with mocks is usually sufficient.

**What to assert:** assert on the observable outcome the feature promises, not on how it gets there internally. Query → returned data matches expectations. Command → side effect occurred (DB state changed, event published). Transformation → output structure and values are correct. If you're unsure, ask: "what would a user notice if this broke?"

**When a mock is dangerous:** a mock is an assumption about how something else behaves. Mocking a stable in-process interface (ILogger, ITimeProvider) is always safe. Mocking a boundary where the contract can drift without your code changing (database queries, serialization formats, HTTP integrations) is where bugs hide. When the mock's fidelity matters to correctness, cross the boundary in a test instead.

## Key files

- `.githooks/pre-commit` — commit-time enforcement
- `.githooks/reference-transaction` — branch-creation enforcement
- `.claude/hooks/block-main-branch.sh` — edit-time main/master protection
- `.claude/hooks/block-merged-branch.sh` — shared merged-branch check
- `.claude/settings.json` — Claude Code hook registration
- `Directory.Build.props` — `TreatWarningsAsErrors`, analyzer project wire-up
- `.editorconfig` + `tests/.editorconfig` — style + severity overrides
