---
name: build-and-submit
description: Use for EAS builds and App Store submission - production build, eas submit, attaching subscriptions to the version, submitting for review (Stage 5 of SUBMISSION.md)
---

# Build & Submit (EAS CLI + Playwright for the version page)

## Preconditions

- SUBMISSION.md Stages 0-4 all checked.
- `npm run doctor` passes (run it now; if it fails, route to
  submission-doctor and stop).
- `eas whoami` succeeds. If not, ask the user to run `! eas login`.

## Build

1. If `app.config.js` `extra.eas.projectId` is missing, run `eas init` and
   commit the change (follow the dev lifecycle in CLAUDE.md - issue, branch,
   PR).
2. Run `eas build --platform ios --profile production --non-interactive`.
   First-ever build needs Apple credentials setup, which is interactive:
   if the command fails asking for credentials, ask the user to run
   `! eas build --platform ios --profile production` themselves once, then
   resume here.
3. Stream/poll the build with `eas build:list --platform ios --limit 1`.
   On failure, fetch the log URL it prints, read the failing phase, report
   the root cause, and fix before retrying.

## Submit

4. Run `eas submit --platform ios --latest`. This uploads the build to ASC.
5. Wait for the build to finish processing in ASC (TestFlight tab shows it;
   processing can take 5-30 min). Poll via asc-setup style Playwright reads.

## Attach IAP + metadata (Playwright)

6. On the ASC version page: create the version if ASC has not auto-created
   one; select the processed build.
7. In-App Purchases and Subscriptions section: attach the subscriptions
   recorded in SUBMISSION.md Stage 3. This satisfies Apple's
   first-subscription-ships-with-a-version rule.
8. Version metadata: description, keywords, support URL, screenshots
   (6.7" and 6.1" minimum), what's new. Ask the user for anything missing.
9. Export compliance: already declared via ITSAppUsesNonExemptEncryption
   in app.config.js, confirm no prompt blocks submission.

## Submit for review

10. Press "Add for Review" / "Submit to App Review". Snapshot the
    confirmation state.
11. Update SUBMISSION.md: check Stage 5 items and the Stage 6 "Submitted"
    box; record the version number.
12. Tell the user review typically takes 24-72h and that the asc-setup
    skill checks status on request.
