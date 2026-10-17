# Native test distribution

The Android download is a standalone, signed ARM64 release APK with its JavaScript and font assets bundled. It connects to the HTTPS Toushir demonstration API. It does not need Metro, Expo Go or a development computer. Installing a downloaded APK requires Android's normal installation consent. This is test distribution, not a Play Store release.

The signing key is dedicated to this preview and retained privately outside source control. Keep the same key and raise `versionCode` for updates. Replacing the key requires uninstalling the previous app. Never place the keystore or its credentials in the repository, APK download folder or public hosting environment.

The pricing release packages are version 1.1.0, Android `versionCode` 2, and contain reviewed-basket consent protocol 1. Both were compiled sequentially with one worker and a 1 GB Java heap, then checked for signature, stable preview certificate, package identity, bundled protocol, HTTPS origin, ARM64 libraries, archive integrity and SHA256. These builds were not launched in a native simulator or on a device. Earlier native runtime results retain their original scope; current workflow acceptance uses the web previews. See `verification/pricing/native-package-build.json` for the actual build evidence.

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

From the repository root, run `scripts/build-android-release.zsh merchant`, then `scripts/build-android-release.zsh driver`. The default is one worker and a 1024 MB Java heap; `TOUSHIR_BUILD_WORKERS` and `TOUSHIR_JAVA_HEAP` can override those limits. The script preserves each APK separately as `apps/mobile/build/downloads/toushir-merchant.apk` and `toushir-driver.apk`. Check each signature, package identity, bundled JavaScript, API origin, native-library architectures and SHA256 before distribution. The preview targets ARM64 Android devices; the minimum and target Android SDK versions are recorded in the actual package verification report.

The iOS simulator compilation is a separate verification. An iOS simulator app cannot be installed on an iPhone. Device distribution through TestFlight or the App Store requires the owner's Apple developer signing and distribution setup. The browser version is available independently on iPhone.

The approved staging service at https://toushir-commerce-staging.onrender.com uses persistent PostgreSQL in an isolated application schema. Free web compute can sleep and take time to wake; native installation does not change that limit. The free test database expires on 15 November 2026 at 17:52 UTC. Export and migrate, or explicitly upgrade, before expiry. This remains a labelled test deployment; configure actual business terms and a durable commercial hosting plan before real operations.
