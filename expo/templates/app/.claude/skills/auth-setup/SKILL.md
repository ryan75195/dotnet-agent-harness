---
name: auth-setup
description: Use for Auth0 configuration - native application, callback/logout URLs, the Apple connection, and writing the public env vars (part of Stage 1 of SUBMISSION.md, only when the app uses auth)
---

# Auth0 Setup (Playwright-driven)

Drives https://manage.auth0.com via Playwright MCP browser tools. Only run this
when the app uses auth (the user wants a login).

## Preconditions

- Read SUBMISSION.md first.
- If a login screen appears, STOP and ask the user to log in, then continue.

## Idempotency protocol (mandatory)

Snapshot before creating anything. If the application/connection already exists,
verify its configuration matches SUBMISSION.md, record what is missing, and only
create the missing pieces. On unexpected page state: stop, snapshot, report.

## Steps

1. Applications → create (or reuse) a Native application named after the app.
2. Settings: record the Domain and Client ID. Set the Allowed Callback URLs and
   Allowed Logout URLs to the app's redirect (the Expo scheme redirect, e.g.
   `apptemplate://redirect` for the template, or the scaffolded app's scheme).
   Cross-check the scheme against `scheme` in app.config.js.
3. Enable a Refresh Token grant (the template requests `offline_access`).
4. Authentication → Social → add and enable the Apple connection, then enable it
   for this application. This is REQUIRED: the template ships native Sign in with
   Apple, and Apple rejects apps offering other social logins without it. Ask the
   user for the Apple Services ID / key if Auth0 prompts — pause and never handle
   secrets in the repo.
5. Write the public values to `.env.production` and `.env.local`:
   `EXPO_PUBLIC_AUTH0_DOMAIN=<domain>` and `EXPO_PUBLIC_AUTH0_CLIENT_ID=<clientId>`.
   These files are gitignored — confirm with `git check-ignore .env.production`
   before writing. Never write the Auth0 client secret into the repo (the native
   PKCE flow does not need it).
6. Update SUBMISSION.md: record the domain, client ID, and callback URL; check off
   the Stage 1 auth items.
7. Run `npm run doctor` — the auth checks should pass.
