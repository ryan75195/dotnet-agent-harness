# TVAppTemplate

Expo template for Android TV and Apple TV. It extends the standard Expo app
harness with React Native TV, landscape-first UI, D-pad-visible focus states,
and `expo-video` for playback.

## Quick start

```powershell
.\setup.ps1
npm run verify
npm run prebuild:tv
npm run android:tv
```

Use an Android TV emulator with API 31 or later. Apple TV builds require macOS,
Xcode, and the tvOS SDK. The `@react-native-tvos/config-tv` plugin configures
native projects for TV during prebuild.

## What is TV-specific

- `react-native` aliases `react-native-tvos`, matched to Expo SDK 55. An
  `overrides` block pins the alias for the whole dependency tree — every
  `react-native-tvos` release is a prerelease, which plain semver ranges
  exclude, so without the override npm can silently resolve plain
  `react-native` and every TV focus API becomes a no-op. A guard test
  (`src/lib/tv/__tests__/reactNativeTvos.test.ts`) fails the suite if the
  installed `react-native` is not the TV fork.
- `app.config.js` enables the TV config plugin and locks the UI to landscape.
- `FocusButton` exposes a visible focus ring for D-pad and Siri Remote users.
- `expo-video` is included for media playback.

Keep every actionable control reachable using a remote. Do not rely on touch
gestures, hover-only affordances, or mobile-sized tap targets. Test focus order
on Android TV and Apple TV before releasing.

## Authentication and purchases

The inherited Auth0 browser flow is a mobile-friendly baseline, not a complete
living-room sign-in experience. Production TV apps should add a device-code or
QR-code flow. RevenueCat uses the Apple key on Apple TV and the Android key on
Android TV. A RevenueCat key is required only for EAS store builds
(`EAS_BUILD` is set); local production bundles and sideload builds load
without one.

The submission workflow inherited from the base template is iOS App Store
focused. Treat Android TV publishing and any store-specific metadata as a
separate release checklist.

## Sideload builds (local prebuild)

`expo prebuild` regenerates the gitignored `android/` directory from scratch.
The bundled config plugin `plugins/with-tv-sideload-patches.js`, wired into
`app.config.js`, applies the three sideload patches during every prebuild, so a
fresh prebuild is immediately ready for `gradlew assembleRelease`. The plugin
sets values rather than appending them, so re-running prebuild never
accumulates duplicate entries.

The patches it applies:

- Pins the Gradle wrapper to 8.14.3. The prebuild template writes a Gradle 9
  wrapper, but the react-native-tvos 0.83 gradle plugin references
  `JvmVendorSpec.IBM_SEMERU`, which Gradle 9 removed, so `assembleRelease`
  fails with `Class org.gradle.jvm.toolchain.JvmVendorSpec does not have
  member field 'IBM_SEMERU'` on an unpinned wrapper.
- Adds `-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI`
  to the Kotlin compile arguments in `android/build.gradle`. expo-video's
  `VideoModule` is annotated `@UnstableReactNativeAPI`, an opt-in annotation
  at error level, and the expo autolinking plugin does not pass the opt-in
  when compiling against the TV fork, so `:expo:compileReleaseKotlin` fails on
  the generated `ExpoModulesPackageList.kt` without it.
- Permits cleartext HTTP on the generated `<application>` element. Android API
  28+ blocks cleartext, so a sideloaded build pointed at a development API on
  the local network fails every request without it. This deliberately keeps
  the app-wide flag from the previous manual patch instead of a scoped
  network-security-config limited to the hosts that need it; scoping the rule
  changes what the patch does and remains tracked in
  open-source-university-tv#19.
