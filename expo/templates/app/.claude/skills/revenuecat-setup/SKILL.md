---
name: revenuecat-setup
description: Use for RevenueCat configuration - project, entitlements, offerings, API keys (Stage 4 of SUBMISSION.md)
---

# RevenueCat Setup (Playwright-driven)

Drives https://app.revenuecat.com via Playwright MCP browser tools.

## Preconditions

- SUBMISSION.md Stages 2 and 3 complete (product IDs recorded — RevenueCat
  imports them from ASC, so they must exist first).
- If a login screen appears, STOP and ask the user to log in, then continue.

## Idempotency protocol (mandatory)

Snapshot before creating anything. If the project/app/entitlement/offering
already exists, verify its configuration matches SUBMISSION.md, record what
is missing, and only create the missing pieces. On unexpected page state:
stop, snapshot, report.

## Steps

1. Create a project named after the app (or reuse the existing one).
2. Add an iOS app to the project with the PRODUCTION bundle ID from
   SUBMISSION.md.
3. App Store Connect API: RevenueCat needs the In-App Purchase Key. Ask the
   user to generate/locate the .p8 In-App Purchase Key in ASC (Users and
   Access → Integrations → In-App Purchase) and upload it in the RevenueCat
   app settings. Pause for the user — never handle the .p8 contents yourself.
4. Products: import the product IDs recorded in SUBMISSION.md Stage 3.
5. Entitlement: create `premium` (must match PREMIUM_ENTITLEMENT in
   src/lib/purchases/useSubscription.ts — cross-check before creating).
   Attach all products to it.
6. Offering: configure the `default` offering with one package per product
   (monthly → $rc_monthly, annual → $rc_annual).
7. Copy the PUBLIC iOS API key (Project settings → API keys → Public
   app-specific). Write it to `.env.production` and `.env.local` as
   `EXPO_PUBLIC_REVENUECAT_IOS_API_KEY=<key>`. These files are gitignored —
   confirm with `git check-ignore .env.production` before writing.
8. Update SUBMISSION.md: record the project name, check off Stage 4.
9. Run `npm run doctor` — the `.env.production` check should now pass.
