#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
role="${1:-merchant}"
platform="${2:-android}"
case "$role" in merchant) export EXPO_PUBLIC_APP_ROLE=MERCHANT;; driver) export EXPO_PUBLIC_APP_ROLE=DRIVER;; *) echo 'Use merchant or driver.' >&2; exit 2;; esac
case "$platform" in android|ios) ;; *) echo 'Use android or ios.' >&2; exit 2;; esac
if [ -z "${EXPO_PUBLIC_API_URL:-}" ]; then echo 'Set EXPO_PUBLIC_API_URL to the running API origin.' >&2; exit 2; fi
export CMAKE_BUILD_PARALLEL_LEVEL=2
npx expo prebuild --platform "$platform" --no-install
if [ "$platform" = android ]; then
 (cd android && ./gradlew assembleRelease --console=plain --max-workers=2 -PreactNativeArchitectures=arm64-v8a -Dorg.gradle.parallel=false -Dorg.gradle.jvmargs='-Xmx2048m -XX:MaxMetaspaceSize=512m')
 mkdir -p build/downloads
 cp android/app/build/outputs/apk/release/app-release.apk "build/downloads/toushir-${role}.apk"
 echo "Built build/downloads/toushir-${role}.apk"
else
 (cd ios && pod install)
 workspaces=(ios/*.xcworkspace)
 workspace="${workspaces[0]}"
 scheme="$(basename "$workspace" .xcworkspace)"
 xcodebuild -workspace "$workspace" -scheme "$scheme" -configuration Release -sdk iphonesimulator -destination "generic/platform=iOS Simulator" -derivedDataPath "build/ios-${role}" -jobs 2 build
fi
