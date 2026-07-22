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

`expo prebuild` regenerates the gitignored `android/` directory from scratch,
so all three patches below must be re-applied after every fresh prebuild
before running `gradlew assembleRelease` for a sideloaded build.

1. Pin Gradle to 8.14.3. The prebuild template writes a Gradle 9 wrapper, but
   the react-native-tvos 0.83 gradle plugin references
   `JvmVendorSpec.IBM_SEMERU`, which Gradle 9 removed, so `assembleRelease`
   fails with `Class org.gradle.jvm.toolchain.JvmVendorSpec does not have
   member field 'IBM_SEMERU'`. In
   `android/gradle/wrapper/gradle-wrapper.properties` set:

   ```properties
   distributionUrl=https\://services.gradle.org/distributions/gradle-8.14.3-bin.zip
   ```

2. Opt in to the unstable React Native API for Kotlin. expo-video's
   `VideoModule` is annotated `@UnstableReactNativeAPI`, an opt-in annotation
   at error level, and the expo autolinking plugin does not pass the opt-in
   when compiling against the TV fork, so `:expo:compileReleaseKotlin` fails
   on the generated `ExpoModulesPackageList.kt`. Append to
   `android/build.gradle`:

   ```gradle
   subprojects {
     tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {
       compilerOptions {
         freeCompilerArgs.add("-opt-in=com.facebook.react.common.annotations.UnstableReactNativeAPI")
       }
     }
   }
   ```

3. Allow cleartext traffic for LAN APIs. Android API 28+ blocks cleartext
   HTTP, so a sideloaded build pointed at a development API on the local
   network fails every request. Add `android:usesCleartextTraffic="true"` to
   the `<application>` element in
   `android/app/src/main/AndroidManifest.xml`.
