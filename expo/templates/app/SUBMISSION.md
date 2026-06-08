# SUBMISSION.md — iOS App Store submission state

Single source of truth for submission progress. Skills read this file to find the
current stage and update it (check boxes, record values) after every completed step.
Stages are strictly ordered — never start stage N+1 with unchecked items in stage N.

## Recorded values

| Key | Value |
|---|---|
| Production bundle ID | _unset_ |
| ASC app ID | _unset_ |
| ASC team ID | _unset_ |
| Subscription group | _unset_ |
| Subscription product IDs | _unset_ |
| RevenueCat project | _unset_ |
| RevenueCat entitlement | premium |
| Auth0 domain | _unset_ |
| Auth0 client ID | _unset_ |
| Auth0 callback URL | _unset_ |
| Account delete endpoint | _unset_ |
| Demo account (review) | _unset_ |
| Privacy policy URL | _unset_ |
| Support URL | _unset_ |

## Stage 0 — Prerequisites (human)

- [ ] Apple Developer Program membership active
- [ ] App Store Connect access confirmed
- [ ] ASC API key (.p8) generated and stored OUTSIDE this repo
- [ ] RevenueCat account created
- [ ] EAS CLI installed and logged in (`eas whoami` succeeds)

## Stage 1 — Local readiness (skill: submission-doctor)

- [ ] App icon (1024x1024 PNG, no alpha) added and referenced in app.config.js
- [ ] Splash image added and referenced in app.config.js
- [ ] Production bundle identifier chosen and set in app.config.js (not com.example.*)
- [ ] Privacy policy URL and support URL recorded above
- [ ] (If using auth) Auth0 tenant + native application configured (skill: auth-setup)
- [ ] (If using auth) Sign in with Apple enabled in Auth0 (required by Apple when any social login is offered)
- [ ] (If using auth) EXPO_PUBLIC_AUTH0_DOMAIN and EXPO_PUBLIC_AUTH0_CLIENT_ID set together in .env.production
- [ ] (If using auth) Account deletion endpoint deployed and EXPO_PUBLIC_ACCOUNT_DELETE_URL set (Apple 5.1.1(v) requires in-app account deletion)
- [ ] `npm run doctor` passes

## Stage 2 — ASC app record (skill: asc-setup)

- [ ] App record created in App Store Connect (name, bundle ID, SKU, primary language)
- [ ] App information complete (subtitle, category, content rights)
- [ ] App Privacy questionnaire completed
- [ ] Review information filled (contact, demo account credentials, notes)
- [ ] ASC app ID recorded above

## Stage 3 — Subscription products in ASC (skill: asc-setup)

- [ ] Subscription group created
- [ ] Each product created (reference name, product ID, duration, price)
- [ ] Localized display name + description per product
- [ ] Review screenshot uploaded per product
- [ ] Product IDs recorded above

GATE: Apple requires the FIRST subscription to be submitted together with a new app
version. Products created here stay "Missing Metadata"/"Ready to Submit" until
Stage 5 attaches them to the version. That is expected — do not retry creation.

## Stage 4 — RevenueCat (skill: revenuecat-setup)

- [ ] RevenueCat project created, iOS app added with the production bundle ID
- [ ] App Store Connect In-App Purchase Key (.p8) uploaded to RevenueCat
- [ ] Entitlement `premium` created
- [ ] Products imported/attached to the entitlement
- [ ] Default offering configured with packages
- [ ] Public iOS API key written to .env.production and .env.local (EXPO_PUBLIC_REVENUECAT_IOS_API_KEY)

## Stage 5 — Build & submit (skill: build-and-submit)

- [ ] `npm run doctor` passes
- [ ] `eas build --platform ios --profile production` succeeds
- [ ] `eas submit --platform ios --latest` succeeds
- [ ] App version created in ASC with the new build attached
- [ ] Subscriptions attached to the version (In-App Purchases section on the version page)
- [ ] Version metadata complete (description, keywords, screenshots, what's new)

## Stage 6 — App Review

- [ ] Submitted for review
- [ ] Review status checked (asc-setup skill can read the status page)
- [ ] Approved / released
