# Native test distribution

The Android download is a standalone, signed ARM64 release APK with its JavaScript and font assets bundled. It connects to the HTTPS Toushir demonstration API. It does not need Metro, Expo Go or a development computer. Installing a downloaded APK requires Android's normal installation consent. This is test distribution, not a Play Store release.

The signing key is dedicated to this preview and retained privately outside source control. Keep the same key and raise `versionCode` for updates. Replacing the key requires uninstalling the previous app. Never place the keystore or its credentials in the repository, APK download folder or public hosting environment.

To reproduce the Android build, install Node.js 24, JDK 17 and the Android SDK/NDK required by the Expo native project, run `npm ci` in `apps/mobile`, and set:

```text
JAVA_HOME
ANDROID_HOME
EXPO_PUBLIC_API_URL=https://your-verified-api.example
TOUSHIR_SIGNING_STORE_FILE=/private/path/to/operator-keystore.p12
TOUSHIR_SIGNING_STORE_PASSWORD
TOUSHIR_SIGNING_KEY_ALIAS
TOUSHIR_SIGNING_KEY_PASSWORD
```

Run `scripts/build-android-release.zsh`. The APK is produced at `apps/mobile/android/app/build/outputs/apk/release/app-release.apk`. Check its signature, manifest, bundled JavaScript, API origin, native-library architectures and SHA256 before distribution. The preview targets ARM64 Android devices; the minimum and target Android SDK versions are recorded in the actual package verification report.

The iOS simulator compilation is a separate verification. An iOS simulator app cannot be installed on an iPhone. Device distribution through TestFlight or the App Store requires the owner's Apple developer signing and distribution setup. The browser version is available independently on iPhone.

The currently hosted free demo can sleep and its SQLite storage is ephemeral. Native installation does not change this hosting limit. Use the durable installation described in the main deployment documentation before processing real customer transactions.
