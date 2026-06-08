---
name: asc-setup
description: Use for App Store Connect work - creating the app record, filling metadata, creating subscription products, or checking review status (Stages 2, 3, and 6 of SUBMISSION.md)
---

# App Store Connect Setup (Playwright-driven)

Drives https://appstoreconnect.apple.com via Playwright MCP browser tools.

## Preconditions

- Read SUBMISSION.md first. Only run the stage it says is next.
- Playwright MCP available. Navigate to App Store Connect; if a login or 2FA
  screen appears, STOP and ask the user to complete login in the browser
  window, then continue.

## Idempotency protocol (mandatory)

Before creating ANYTHING: snapshot the current page and check whether the
object already exists (app record in the Apps list, subscription group on the
Subscriptions page, product in the group). If it exists, verify its fields
match SUBMISSION.md, record its IDs, check the box, and move on. Re-running
any stage must always be safe.

On ANY unexpected page state (selector missing, unfamiliar layout, error
banner): stop, snapshot, report what you saw. Never guess-click.

## Stage 2 — App record

1. Apps → "+" → New App. Platform iOS; Name, Primary Language, Bundle ID
   (must match `ios.bundleIdentifier` in app.config.js — cross-check), SKU
   (use the bundle id), full access.
2. App Information: subtitle, category. Content rights declaration.
3. App Privacy: ask the user which data types the app collects before
   answering the questionnaire — never guess privacy answers.
4. App Review information: contact details, demo account (record the
   credentials location in SUBMISSION.md — credentials themselves go in a
   password manager, NOT in the repo), review notes.
5. Record the ASC app ID (from the URL: /apps/<ID>/) in SUBMISSION.md.
6. Check off completed Stage 2 items.

## Stage 3 — Subscription products

1. App page → Monetization → Subscriptions. Create the subscription group if
   missing (one group unless the user wants tiers to coexist).
2. Per product: Reference Name, Product ID (reverse-DNS style, e.g.
   `monthly_premium` — must match what RevenueCat will import in Stage 4),
   duration, price. Localization: display name + description. Review
   screenshot (1242x2208 or device-equivalent) — ask the user to provide one
   or generate from the running app.
3. Expect final status "Missing Metadata" or "Ready to Submit" — NOT
   "Approved". Apple requires the first subscription to ship with a new app
   version (Stage 5 attaches it). Do not retry; record product IDs and Apple
   IDs in SUBMISSION.md and check off Stage 3.

## Stage 6 — Review status

Navigate to the app's Distribution page and report the current version
status (Waiting for Review / In Review / Rejected / Ready for Sale). If
rejected, open Resolution Center, extract the rejection reasons verbatim,
and report them.
