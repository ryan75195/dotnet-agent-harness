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

**Direct edits and commits to `main` are blocked.** **Edits to an already-merged branch are blocked** (Claude Code and Codex `PreToolUse` hooks + pre-commit).

## Code style

- **No comments.** `NoCommentsAnalyzer` (CI0013) blocks `//`, `/* */`, and `///` at error severity. Extract intent into method names, variable names, or types. If a WHY is genuinely non-obvious (hidden constraint, bug workaround), extract it into a named helper — don't write a comment.
- **`TreatWarningsAsErrors=true`** with `AnalysisLevel=latest-all`. Any CA/IDE/CS/CI diagnostic at severity `error` breaks the build. Exceptions: NU1510 and CI0002 show as non-blocking warnings.
- Full style rules live in `.editorconfig` (root + `tests/` override). Nullable reference types enabled everywhere. File-scoped namespaces, Allman braces, `_camelCase` private fields, `I`-prefixed interfaces.

## Architecture

- **Solution:** `EtlApi.slnx`, .NET 10, four `src/` projects (`Core`, `Api`, `Etl`, `Analyzers`) and four `tests/` projects (`Tests.Unit`, `Tests.Integration`, `Tests.Architecture`, `Tests.Analyzers`).
- **Architecture tests** in `tests/EtlApi.Tests.Architecture/` enforce layering, DI shape, DI wiring (every public Core interface must be registered via `Core.ServiceCollectionExtensions.AddCoreServices()`), naming conventions, and one-public-type-per-file. Tests are split across `LayerDependencyTests`, `NamingConventionTests`, `ServiceShapeTests`, `CodeStructureTests`, and `DiRegistrationTests`; shared infrastructure lives in `TestHelpers.cs`.
- **Custom analyzers** in `src/EtlApi.Analyzers/` enforce CI0001-CI0013 (method length, ctor param count, no tuple returns, no anonymous serialization, no comments, etc).

## Key files

- `.githooks/pre-commit` — commit-time enforcement
- `.githooks/reference-transaction` — branch-creation enforcement
- `.claude/hooks/block-main-branch.sh` — edit-time main/master protection
- `.claude/hooks/block-merged-branch.sh` — shared merged-branch check
- `.claude/hooks/block-mutating-shell-on-main-branch.sh` — shell-time main/master mutation protection
- `.claude/settings.json` — Claude Code hook registration
- `.codex/hooks.json` — Codex hook registration
- `.codex/hooks/*.ps1` — Codex edit-time and mutating-shell guards
- `Directory.Build.props` — `TreatWarningsAsErrors`, analyzer project wire-up
- `.editorconfig` + `tests/.editorconfig` — style + severity overrides

