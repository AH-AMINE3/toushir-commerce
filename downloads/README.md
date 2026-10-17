# Verified native previews

This directory is copied into the web service at `/downloads/`. It contains no APK by default. Never publish an emulator-only build as a public phone build.

After verifying the deployed HTTPS API, build each app using that exact origin and the private local signing key. Copy the resulting APKs as `toushir-merchant.apk` and `toushir-driver.apk`, and update `manifest.json`:

```json
{"merchant":{"apiOrigin":"https://verified-host.example","sha256":"actual-file-hash"},"driver":{"apiOrigin":"https://verified-host.example","sha256":"actual-file-hash"}}
```

`/api/distribution` exposes an app link only when its APK exists and its manifest HTTPS origin matches the current public API. Keystores, passwords and other private signing material never belong in this directory or Git. Publication remains separate from App Store/Play Store distribution.
