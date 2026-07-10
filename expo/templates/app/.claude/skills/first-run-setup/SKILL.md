---
name: first-run-setup
description: Use at session start when config-doctor reports the project is not build/deploy-ready, or when the user asks to set up the project - interviews the user, gets EAS logged in / project linked / iOS credentials provisioned and a real bundle id, offers optional payments/auth/API config, and records decisions so the session-start nudge goes quiet
---

# First-run setup (EAS build/deploy readiness + optional features)

Run this when the SessionStart config check nudges you, or when the user asks
to "set up the project" / "make it build-ready". Goal: clear every critical
item so `eas build` / `eas submit` run without hitting a setup wall, then
record optional-feature decisions.

## Read current state first

Run `node scripts/config-doctor.js` and read its report. Work only the items
it flags. Re-run it at the end - success is when it prints nothing.

## Tier 1 - critical (do these first, in order)

1. **EAS login.** Run `eas whoami`. If not logged in, ask the user to run
   `! eas login`, then re-run `eas whoami` to confirm.
2. **EAS project link.** If `app.config.js` has no `extra.eas.projectId`, run
   `eas init` (or ask the user to run `! eas init` if it needs interactive
   auth). Because this template uses a dynamic `app.config.js`, `eas init`
   may print the project id rather than writing it - if so, set
   `extra.eas.projectId` in `app.config.js` by hand to the id it prints.
   Commit the `app.config.js` change through the CLAUDE.md dev lifecycle
   (issue -> feat branch -> PR), never directly on main.
3. **Production bundle identifier.** If the bundle id still contains
   `com.example.`, ask the user for their real reverse-DNS id (e.g.
   `com.acme.myapp`) and set it in `app.config.js`. Both the production and
   `.dev` values derive from the same string in the template. Commit via the
   lifecycle.
4. **iOS credentials.** Confirm Apple Developer membership + App Store Connect
   access (SUBMISSION.md Stage 0). If missing, tell the user this is a hard
   prerequisite and stop this step - do not loop. Otherwise run
   `! eas credentials` (interactive; needs Apple login) and provision the iOS
   distribution certificate + provisioning profile for the production bundle
   id. Record it in state (below) only after it succeeds.

## Tier 2 - optional features (offer each once)

For each of payments, auth, and API base URL that config-doctor lists as
awaiting a decision, ask the user: enable now or skip for now?

- **Payments (RevenueCat):** if enabling, get the public iOS API key and write
  `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY` to BOTH `.env.local` and
  `.env.production` (create the files if missing). Full store wiring lives in
  the revenuecat-setup skill; this only captures the key.
- **Auth (Auth0):** if enabling, get the domain and client id and write
  `EXPO_PUBLIC_AUTH0_DOMAIN` and `EXPO_PUBLIC_AUTH0_CLIENT_ID` to both env
  files. Auth also requires `EXPO_PUBLIC_ACCOUNT_DELETE_URL` (Apple 5.1.1(v))
  and the `expo-apple-authentication` plugin (already in app.config.js) -
  capture the delete URL too, or record it as the next blocker. Deep Auth0
  tenant setup lives in the auth-setup skill.
- **API base URL:** if enabling, write `EXPO_PUBLIC_API_BASE_URL` to both env
  files.

If the user skips a feature, record it as `deferred` so it is not offered
again.

## Record state

Write `.claude/.setup-state.json` (gitignored):

    {
      "version": 1,
      "iosCredentials": "provisioned",
      "features": { "payments": "enabled", "auth": "deferred", "api": "deferred" }
    }

- Set `iosCredentials` to `"provisioned"` only after Tier 1 step 4 succeeds;
  omit the key until then.
- Set each feature to `"enabled"` or `"deferred"` per the user's choice. A
  feature whose env keys are already set counts as enabled even without an
  entry.

## Confirm

Re-run `node scripts/config-doctor.js`. If it prints nothing, the project is
build/deploy-ready and every optional decision is recorded. If it still
nudges, resolve the remaining flagged items.
