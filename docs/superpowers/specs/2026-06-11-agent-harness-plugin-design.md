# Agent Harness Plugin — Design

Date: 2026-06-11
Status: Approved pending review

## Overview

A Claude Code plugin, living in this repo, that turns the templates from
"things you copy once" into an operated harness:

1. **Scaffolding skills** create new projects from the templates and stamp
   them with provenance.
2. **Feedback capture** instruments the existing git-hook gate so that
   guardrail failures (and their eventual fixes) accumulate in a central
   store for later analysis.
3. **`harness-update` skill** pulls the latest harness repo changes into a
   previously-scaffolded project, using PowerShell scripts for the
   deterministic work and the agent for judgment calls.

## Non-goals (deferred, not rejected)

- **Synthesis**: no automated turning of captured feedback into new rules
  or template PRs. Events accumulate; a synthesis skill can be added later.
- **Capture outside harness projects**: no plugin-level capture hooks for
  foreign codebases. Capture lives in the template git hooks only.
- **Retrofit skill** for non-template projects.

## Repo layout changes

```
dotnet-agent-harness/
├── .claude-plugin/marketplace.json     # NEW — makes the repo installable as a marketplace
├── plugin/                             # NEW — the plugin itself
│   ├── .claude-plugin/plugin.json      # name: agent-harness
│   ├── skills/
│   │   ├── new-dotnet-cli/SKILL.md
│   │   ├── new-dotnet-etl-api/SKILL.md
│   │   ├── new-expo-app/SKILL.md
│   │   └── harness-update/SKILL.md
│   ├── hooks/hooks.json                # PostToolUse annotation hook
│   ├── hooks/feedback-annotate.ps1
│   └── scripts/
│       ├── write-stamp.ps1
│       ├── get-update.ps1
│       ├── apply-update.ps1
│       └── harness-note.ps1
├── dotnet/templates/*/harness-manifest.json   # NEW — harness-owned paths
├── expo/templates/app/harness-manifest.json   # NEW
└── (template .githooks/ and CLAUDE.md gain capture + routing additions)
```

Local install for development: `claude --plugin-dir .\plugin`. Public
install: marketplace source `github: ryan75195/dotnet-agent-harness`, then
`/plugin install agent-harness@<marketplace>`.

## Component 1 — Scaffolding skills + stamp

Three skills, one per template. Each one:

1. Locates the harness repo: configured local checkout first
   (`~/.agent-harness/config.json`, key `repoPath`), else shallow-clones
   `https://github.com/ryan75195/dotnet-agent-harness` to a temp dir.
2. Runs the existing scaffold path verbatim:
   - .NET: `dotnet new install <repo>/dotnet` (if needed) →
     `dotnet new cli|etl-api -n <Name>` → `setup.ps1`
   - Expo: `expo/new-app.ps1 -Name <Name>` → `setup.ps1`
3. Calls `write-stamp.ps1` to create `.harness.json` in the project root.
4. Verifies the result the harness way: `dotnet build` + `dotnet test`, or
   `npm run verify`. No success claim without that output.

### `.harness.json` (committed to the project)

```json
{
  "template": "expo-app",
  "stack": "expo",
  "repoUrl": "https://github.com/ryan75195/dotnet-agent-harness",
  "scaffoldCommit": "<sha of harness repo main at scaffold time>",
  "lastUpdateCommit": "<sha, initially = scaffoldCommit>",
  "renames": { "AppTemplate": "MyApp", "app-template": "my-app" },
  "scaffoldedAt": "2026-06-11T00:00:00Z"
}
```

`renames` records the token substitutions the scaffold performed (from
`.template.config` sourceName for .NET; from `new-app.ps1`'s replacement
map for Expo) so updates can re-apply them to incoming template files.

## Component 2 — Feedback capture via the git gate

### Why the git gate

Mid-session test failures are the expected red phase of TDD — noise. A
pre-commit failure means the agent believed the work was complete and a
guardrail caught something anyway: exactly the population of mistakes worth
mining for new rules. Git hooks also fire for any agent (Claude Code,
Codex) and for manual commits.

### Capture (template `.githooks/pre-commit`, both stacks)

On any gate failure, before `exit 1`, the hook:

1. Generates an event id (short random hex).
2. Appends one JSON line to `~/.agent-harness/feedback/events.jsonl`:

```json
{
  "id": "a1b2c3",
  "ts": "2026-06-11T14:02:11Z",
  "project": "C:/Users/ryan7/programming/my-app",
  "template": "expo-app",
  "gate": "lint",
  "branch": "feat/12-paywall",
  "outputTail": "<last ~50 lines of the failing check>",
  "note": null,
  "fixCommit": null
}
```

   `gate` is one of: `typecheck | lint | depcruise | test-files | tests`
   (Expo) / `build | test | analyzers` (.NET) — the hook knows which check
   failed, so events arrive pre-classified.
3. Writes the staged diff to
   `~/.agent-harness/feedback/diffs/<id>.failure.patch` (sidecar file —
   diffs are too big for JSONL lines).
4. Records the pending id in `.git/harness-pending-event`.
5. Prints the marker as its final output line:
   `HARNESS-FEEDBACK: event a1b2c3 logged — append a one-line note on what this code was trying to do.`

The logging logic is inline in the template hooks (bash, runs under Git
for Windows) — self-contained, no plugin dependency. Capture must never
block work: logging failures are swallowed (`|| true`), and the hook's
exit code is always the gate's own.

### Fix linkage (template `.githooks/post-commit`, new)

If `.git/harness-pending-event` exists, the post-commit hook writes the
commit SHA into that event's `fixCommit` field (by appending a
`{"id":..., "fixCommit":...}` patch line — the store is append-only;
readers take the last record per id), saves the commit diff to
`diffs/<id>.fix.patch`, and deletes the pending file. "Here is the
mistake, here is what the fix looked like" is the richest input for any
future synthesis.

### Annotation (plugin hook)

`plugin/hooks/hooks.json` registers a PostToolUse hook on Bash/PowerShell.
`feedback-annotate.ps1` scans tool output for the `HARNESS-FEEDBACK:
event <id>` marker; on match it returns additionalContext instructing the
agent:

> A harness feedback event `<id>` was just logged. Append a one-line note
> describing what the failing code was trying to do:
> `& <plugin>/scripts/harness-note.ps1 -EventId <id> -Note "..."`. Then
> continue fixing the failure.

`harness-note.ps1` appends a `{"id":..., "note":...}` patch line to the
store. If annotation never happens (plugin absent, agent ignores it), the
event still stands on its own — annotation is opportunistic, capture is
enforced.

## Component 3 — `harness-update` skill

Run inside a previously-scaffolded project. Flow:

1. **Read the stamp.** Missing stamp → offer backfill: ask which template
   the project came from (or infer by diffing against template history),
   write `.harness.json` with the best-guess commit, then proceed.
2. **`get-update.ps1`** — deterministic:
   - Resolve the harness repo (local checkout → shallow clone fallback).
   - `git diff --name-status <lastUpdateCommit>..origin/main -- <templateDir>`
   - Filter to the template's `harness-manifest.json` globs.
   - For each candidate file, decide:
     - **clean**: the project's current file content equals the
       `lastUpdateCommit` version of the template file (renames applied) —
       the project never touched it → safe to overwrite.
     - **modified**: contents differ → flag for the agent.
     - **new**: file doesn't exist in the project → safe to add.
   - Emit a JSON manifest of `{path, status, action}` for the skill.
3. **`apply-update.ps1`** — copies all `clean`/`new` files into the
   project with the stamp's rename map applied.
4. **Agent resolves flagged files** — three-way judgment per file: old
   template version (base), new template version, project's customized
   version. The agent merges, explains each decision, and asks the user
   only when a customization genuinely conflicts with the update's intent.
5. **Verify**: run the project's own gate (`npm run verify` /
   `dotnet build && dotnet test`). Failures are fixed before proceeding —
   the update is not done until the gate passes.
6. **Stamp + commit**: set `lastUpdateCommit` to the fetched HEAD, then
   commit through the project's normal lifecycle (issue → feat branch →
   PR). The skill obeys the same hooks it just updated.

### `harness-manifest.json` (per template, in this repo)

Declares harness-owned paths — the boundary between "harness
infrastructure" (updatable) and "the user's app" (never touched):

- Expo: `.githooks/**`, `.claude/**`, `eslint-rules/**`,
  `eslint.config.js`, `.dependency-cruiser.cjs`, `scripts/**`,
  `tsconfig.json`, `.github/workflows/ci.yml`, `CLAUDE.md`
- .NET: `.githooks/**`, `.claude/**` (and `.codex/**` for etl-api),
  analyzer/props files, `.editorconfig`, `CLAUDE.md`

`src/**` is never in a manifest. `CLAUDE.md` is owned but will commonly be
flagged `modified` (projects accrue their own context) — expected; the
agent merges harness sections and preserves project sections.

## Component 4 — CLAUDE.md awareness

Template CLAUDE.md (both stacks) gains a **Harness maintenance** section,
scaffolded into every new project:

- "Update the harness / pull latest template changes / update guardrails"
  → invoke `/agent-harness:harness-update`.
- `.harness.json` records template provenance — never hand-edit or delete.
- When a blocked commit prints `HARNESS-FEEDBACK: event <id>`, append a
  one-line note via `harness-note.ps1` describing what the code was trying
  to do, then fix the failure.

The plugin hook (Component 2) enforces the annotation behavior regardless;
the CLAUDE.md section makes the convention visible and covers agents
running without the plugin's hooks. Doc explains, hook enforces.

## Error handling

- Capture is fail-open: hook logging errors never change the gate's exit
  code or block a commit.
- `get-update.ps1` with no network and no local checkout → clear error,
  no partial state.
- `apply-update.ps1` runs on a feat branch (lifecycle-enforced), so a bad
  update is always revertible; it never writes outside manifest paths.
- Update interrupted mid-way: stamp is only bumped in step 6, so re-running
  the skill recomputes the same manifest idempotently.

## Testing

- **Template tests** (existing harness pattern, seeded violations):
  extend `scaffold-and-validate` to assert (a) `.harness.json` is written,
  (b) a seeded guardrail violation + commit attempt appends an event with
  the right `gate` and a `HARNESS-FEEDBACK` marker in hook output, (c) the
  subsequent passing commit fills `fixCommit`.
- **Update scripts** (Pester): clean/modified/new classification, rename
  re-application, manifest filtering, idempotent re-run.
- **CI**: `template-ci.yml` gains a job running the above plus a smoke
  check that plugin skill frontmatter parses.

## Decisions log

- Capture point: git pre-commit gate, not Claude PostToolUse on test
  commands — higher signal (post-"done" failures only), agent-agnostic.
- Synthesis deferred entirely; store is append-only JSONL + diff sidecars.
- Update boundary is declared per-template in `harness-manifest.json`,
  not inferred.
- Harness repo resolution: configured local checkout, shallow-clone
  fallback.
- Plugin lives in this repo under `plugin/`; repo doubles as its
  marketplace.
