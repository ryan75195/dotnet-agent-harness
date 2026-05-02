# M1 Cleanup Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drop stale references, add fail-open guards, and seed empty test projects with smoke tests so the repo is template-ready.

**Architecture:** Five mechanical edits across four files plus two new smoke-test fixtures. No new abstractions. No behavior changes beyond the deleted test and the `reference-transaction` fail-open paths.

**Tech Stack:** C# / .NET 10, NUnit, FluentAssertions, NetArchTest, bash hooks, GitHub CLI (`gh`).

**Spec:** `docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md` (M1 section)
**Umbrella issue:** #13

---

## File map

**Modify:**
- `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs` — drop `Search`/`Scraping` namespace entries, delete one test, drop stale `AllowedSuffixes`
- `.githooks/pre-commit` — drop `--filter "TestCategory!=RequiresQdrant"`
- `.githooks/reference-transaction` — add fail-open guards at top

**Create:**
- `tests/GlobalRealEstate.Tests.Unit/SmokeTests.cs`
- `tests/GlobalRealEstate.Tests.Integration/SmokeTests.cs`

---

## Pre-flight

- [ ] **Step 0a: Confirm starting branch is `main`**

Run:
```bash
git checkout main && git pull --ff-only
```

Expected: clean fast-forward (or already up-to-date).

- [ ] **Step 0b: Confirm test count baseline and capture analyzer count**

Run:
```bash
dotnet build && dotnet test --no-build --verbosity minimal
```

Verify and record:
- `GlobalRealEstate.Tests.Architecture` must show `Passed: 21`. If different, **stop** — the spec assumes 21 architecture tests. Investigate before proceeding.
- `GlobalRealEstate.Tests.Analyzers`: **record this number** (call it `ANALYZER_BASELINE`). Step 6a verifies it's unchanged.
- `GlobalRealEstate.Tests.Unit` and `GlobalRealEstate.Tests.Integration` either show `Passed: 0` or "No test is available" — both expected, both fine.

---

## Task 1: Open issue and create feat branch

**Files:** none (GitHub + git operations)

- [ ] **Step 1a: Create the M1 issue on GitHub**

Run:
```bash
gh issue create --title "Cleanup: drop stale references and add fail-open guards before templating" --body "Part of #13 (template extraction). Drops Search/Scraping refs, Crawler/Synthesiser suffixes, RequiresQdrant filter; adds fail-open guards to reference-transaction; adds smoke tests so empty test projects don't fail the architecture suite. See plan: docs/superpowers/plans/2026-04-25-m1-cleanup-pass.md"
```

Expected: prints the issue URL. **Capture the issue number** — it's used as `<N>` in the branch name.

- [ ] **Step 1b: Create the feat branch**

Run (substitute `<N>` with the issue number from Step 1a):
```bash
git checkout -b feat/<N>-template-cleanup-pass
```

Expected: switches to new branch. The `reference-transaction` hook fires and confirms the issue exists on GitHub.

- [ ] **Step 1c: Verify hook didn't reject the branch**

Run:
```bash
git branch --show-current
```

Expected: prints `feat/<N>-template-cleanup-pass`. If you got an error from the hook, double-check the issue number matches.

---

## Task 2: Clean up `ArchitectureTests.cs` (3 edits, one commit)

**Files:**
- Modify: `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs`

These three edits all touch the same file and form one logical unit ("remove dead references from prior codebase"). They go in one commit.

- [ ] **Step 2a: Drop `Search` and `Scraping` from `ServiceNamespaces`**

Open `tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs`. Find lines 18–23:

```csharp
    private static readonly string[] ServiceNamespaces =
    [
        "GlobalRealEstate.Core.Data",
        "GlobalRealEstate.Core.Search",
        "GlobalRealEstate.Core.Scraping"
    ];
```

Replace with:

```csharp
    private static readonly string[] ServiceNamespaces =
    [
        "GlobalRealEstate.Core.Data"
    ];
```

- [ ] **Step 2b: Delete `Should_keep_models_free_of_search_and_scraping_dependencies`**

Find the test method at lines 51–61 (just below `Should_keep_models_free_of_data_layer_dependencies`):

```csharp
    [Test]
    public void Should_keep_models_free_of_search_and_scraping_dependencies()
    {
        Types.InAssembly(CoreAssembly)
            .That().ResideInNamespaceContaining("Core.Models")
            .ShouldNot().HaveDependencyOnAny(
                "GlobalRealEstate.Core.Search",
                "GlobalRealEstate.Core.Scraping")
            .GetResult().IsSuccessful.Should().BeTrue(
                "Domain models must not reference search or scraping infrastructure");
    }
```

Delete the entire method (and the blank line after it).

- [ ] **Step 2c: Drop `Crawler`, `Downloader`, `Chunker`, `Synthesiser` from `AllowedSuffixes`**

Find lines 272–279:

```csharp
    private static readonly HashSet<string> AllowedSuffixes =
    [
        "Service", "Repository", "Client", "Store", "Context",
        "Entity", "Command", "Parser", "Crawler", "Downloader",
        "Converter", "Pool", "Worker", "Process", "Extensions",
        "Chunker", "Mapper", "Extractor", "Probe", "Result", "Monitor",
        "Synthesiser", "Plugin", "Filter"
    ];
```

Replace with:

```csharp
    private static readonly HashSet<string> AllowedSuffixes =
    [
        "Service", "Repository", "Client", "Store", "Context",
        "Entity", "Command", "Parser", "Converter", "Pool",
        "Worker", "Process", "Extensions", "Mapper", "Extractor",
        "Probe", "Result", "Monitor", "Plugin", "Filter"
    ];
```

- [ ] **Step 2d: Build to verify the file still compiles**

Run:
```bash
dotnet build tests/GlobalRealEstate.Tests.Architecture/
```

Expected: `Build succeeded`.

- [ ] **Step 2e: Run architecture tests; expect 20 passed**

Run:
```bash
dotnet test tests/GlobalRealEstate.Tests.Architecture/ --no-build --verbosity minimal
```

Expected: `Passed: 20` (down from 21 — the deleted test).

- [ ] **Step 2f: Commit**

Run:
```bash
git add tests/GlobalRealEstate.Tests.Architecture/ArchitectureTests.cs
git commit -m "$(cat <<'EOF'
refactor: drop stale Search/Scraping/Crawler/Synthesiser references

Search and Scraping namespaces don't exist; the test guarding them is
dead. Crawler, Downloader, Chunker, Synthesiser suffixes were inherited
from a prior codebase. Removed both before extracting the template so
they don't propagate to scaffolded projects.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit hook runs (build, format, tests). All pass. Commit lands.

---

## Task 3: Remove `RequiresQdrant` filter from pre-commit hook

**Files:**
- Modify: `.githooks/pre-commit`

- [ ] **Step 3a: Edit the test filter line**

Open `.githooks/pre-commit`. Find line 99:

```bash
  TEST_OUTPUT=$(dotnet test "$proj" --no-build --verbosity quiet --filter "TestCategory!=RequiresQdrant" 2>&1)
```

Replace with:

```bash
  TEST_OUTPUT=$(dotnet test "$proj" --no-build --verbosity quiet 2>&1)
```

- [ ] **Step 3b: Verify the hook still parses**

Run:
```bash
bash -n .githooks/pre-commit
```

Expected: no output (script syntax is valid).

- [ ] **Step 3c: Commit**

Run:
```bash
git add .githooks/pre-commit
git commit -m "$(cat <<'EOF'
chore: drop RequiresQdrant test filter from pre-commit

No Qdrant code in this repo. Removing before templating so scaffolded
projects don't inherit a filter for a category they'll never use.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit fires (now without the dead filter), all checks pass.

---

## Task 4: Add fail-open guards to `reference-transaction`

**Files:**
- Modify: `.githooks/reference-transaction`

- [ ] **Step 4a: Add the guards**

Open `.githooks/reference-transaction`. Find the existing block at the top:

```bash
[ "$1" = "prepared" ] || exit 0

ZERO_OID="0000000000000000000000000000000000000000"
```

Replace with:

```bash
[ "$1" = "prepared" ] || exit 0

# Fail open if there's no GitHub remote yet — local-only repos shouldn't be blocked
git remote get-url origin >/dev/null 2>&1 || exit 0
command -v gh >/dev/null 2>&1 || exit 0

ZERO_OID="0000000000000000000000000000000000000000"
```

- [ ] **Step 4b: Verify the hook still parses**

Run:
```bash
bash -n .githooks/reference-transaction
```

Expected: no output.

- [ ] **Step 4c: Commit**

Run:
```bash
git add .githooks/reference-transaction
git commit -m "$(cat <<'EOF'
fix: fail-open reference-transaction when no remote or gh installed

Without this, a freshly-scaffolded project can't create its first feat
branch until the user has also created a GitHub remote. The hook still
enforces issue-linked branches once a remote exists.

Mirrors the fail-open pattern in block-merged-branch.sh.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes.

---

## Task 5: Add smoke tests to `Tests.Unit` and `Tests.Integration`

**Files:**
- Create: `tests/GlobalRealEstate.Tests.Unit/SmokeTests.cs`
- Create: `tests/GlobalRealEstate.Tests.Integration/SmokeTests.cs`

Both projects currently contain only `AssemblyMarker.cs`. The architecture tests assert `testFixtures.Should().NotBeEmpty()` and `testMethods.Should().NotBeEmpty()`, which fail on a fresh scaffold. Each smoke test is the minimum acceptable content.

- [ ] **Step 5a: Create `tests/GlobalRealEstate.Tests.Unit/SmokeTests.cs`**

```csharp
namespace GlobalRealEstate.Tests.Unit;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void Should_pass_smoke_check() => Assert.Pass();
}
```

- [ ] **Step 5b: Create `tests/GlobalRealEstate.Tests.Integration/SmokeTests.cs`**

```csharp
namespace GlobalRealEstate.Tests.Integration;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void Should_pass_smoke_check() => Assert.Pass();
}
```

- [ ] **Step 5c: Build and run all tests**

Run:
```bash
dotnet build && dotnet test --verbosity minimal
```

Expected: every test project passes. Architecture project = 20. Unit = 1. Integration = 1. Analyzers = (existing count, unchanged).

- [ ] **Step 5d: Commit**

Run:
```bash
git add tests/GlobalRealEstate.Tests.Unit/SmokeTests.cs tests/GlobalRealEstate.Tests.Integration/SmokeTests.cs
git commit -m "$(cat <<'EOF'
test: add smoke fixtures to Tests.Unit and Tests.Integration

Empty test projects fail the architecture-test NotBeEmpty assertions
on a fresh scaffold. A single Should_pass_smoke_check in each is the
minimum that lets a freshly-scaffolded project run dotnet test cleanly.

Users replace these as they write real tests.

Part of #13.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit fires, all tests pass.

---

## Task 6: Final verification and PR

**Files:** none (verification + GitHub op)

- [ ] **Step 6a: Confirm full test counts**

Run:
```bash
dotnet test --verbosity minimal 2>&1 | grep -E "Passed!|Failed!"
```

Expected per-project counts:
- `GlobalRealEstate.Tests.Architecture`: **Passed: 20**
- `GlobalRealEstate.Tests.Unit`: **Passed: 1**
- `GlobalRealEstate.Tests.Integration`: **Passed: 1**
- `GlobalRealEstate.Tests.Analyzers`: **Passed: must equal `ANALYZER_BASELINE` from Step 0b** (the analyzer suite was not touched, so this number must match exactly)

If counts don't match, **stop and investigate** before opening the PR.

- [ ] **Step 6b: Confirm clean working tree**

Run:
```bash
git status
```

Expected: `nothing to commit, working tree clean`.

- [ ] **Step 6c: Push branch**

Run:
```bash
git push -u origin HEAD
```

Expected: branch pushed, `gh` URL printed.

- [ ] **Step 6d: Open PR**

Run (substitute `<N>` with the issue number):
```bash
gh pr create --base main --title "Cleanup: drop stale references and add fail-open guards before templating" --body "$(cat <<'EOF'
Closes #<N>. Part of #13 (template extraction).

## Changes

- Drops `Search`/`Scraping` namespaces from `ServiceNamespaces` array
- Deletes `Should_keep_models_free_of_search_and_scraping_dependencies` test (referenced namespaces that don't exist)
- Drops `Crawler`, `Downloader`, `Chunker`, `Synthesiser` from `AllowedSuffixes`
- Removes `--filter "TestCategory!=RequiresQdrant"` from `.githooks/pre-commit`
- Adds fail-open guards to `.githooks/reference-transaction` (no remote / no gh → exit 0)
- Adds `SmokeTests` to `Tests.Unit` and `Tests.Integration`

## Test plan

- [ ] Architecture tests: 20 passed (was 21; one deleted)
- [ ] Unit tests: 1 passed (smoke)
- [ ] Integration tests: 1 passed (smoke)
- [ ] Analyzer tests: unchanged from baseline
- [ ] Pre-commit hook still works (verified by every commit on this branch passing the hook)

Plan: `docs/superpowers/plans/2026-04-25-m1-cleanup-pass.md`
Spec: `docs/superpowers/specs/2026-04-25-etl-api-template-extraction-design.md`
EOF
)"
```

Expected: PR URL printed.

- [ ] **Step 6e: Squash-merge once approved**

Run (substitute `<N>` with the PR number):
```bash
gh pr merge <N> --squash --delete-branch
```

Expected: PR merged, branch deleted locally and on remote.

---

## Done

After Step 6e, M1 is complete. Next milestone is M2 (split architecture tests) — start a new writing-plans cycle for that milestone.
