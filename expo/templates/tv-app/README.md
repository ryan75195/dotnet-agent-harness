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

- `react-native` aliases `react-native-tvos`, matched to Expo SDK 55.
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
Android TV. EAS supplies the build platform automatically; set
`EXPO_TV_PLATFORM=android` when validating an Android production config
outside EAS.

The submission workflow inherited from the base template is iOS App Store
focused. Treat Android TV publishing and any store-specific metadata as a
separate release checklist.
