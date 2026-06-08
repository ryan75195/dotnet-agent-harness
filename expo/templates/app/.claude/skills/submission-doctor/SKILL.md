---
name: submission-doctor
description: Use when checking App Store submission readiness, when asked "are we ready to submit", or before starting any submission stage
---

# Submission Doctor

Audits local submission readiness and reports the current stage.

## Steps

1. Run `npm run doctor`. For every FAIL line, explain the fix concretely
   (which file, which field, what value).
2. Read `SUBMISSION.md`. Find the FIRST stage that still has unchecked items.
3. Report to the user:
   - Doctor results (pass/fail summary)
   - Current stage and its remaining unchecked items
   - The skill that owns the next stage:

| Stage | Skill |
|---|---|
| 0 — Prerequisites | human task; verify with `eas whoami` and by asking the user |
| 1 — Local readiness | this skill (doctor) + direct edits |
| 2 — ASC app record | asc-setup |
| 3 — Subscription products | asc-setup |
| 4 — RevenueCat | revenuecat-setup |
| 5 — Build & submit | build-and-submit |
| 6 — App Review | build-and-submit |

## Rules

- Never check a SUBMISSION.md box without verifying the underlying fact
  (file exists, command succeeds, page state observed).
- Never start a stage while an earlier stage has unchecked items.
- If the doctor passes but SUBMISSION.md disagrees (or vice versa), trust the
  verification, fix SUBMISSION.md, and tell the user what was stale.
