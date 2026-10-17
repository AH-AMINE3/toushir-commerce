#!/bin/zsh
set -euo pipefail

TASK_ROOT="${0:A:h:h}"
: "${EXPO_PUBLIC_API_URL:?Set the verified public HTTPS API origin.}"
[[ "$EXPO_PUBLIC_API_URL" == https://* ]] || { print -u2 'The release API origin must use HTTPS.'; exit 1; }
: "${TOUSHIR_SIGNING_STORE_FILE:?Set the private signing keystore path.}"
: "${TOUSHIR_SIGNING_STORE_PASSWORD:?Set the keystore password.}"
: "${TOUSHIR_SIGNING_KEY_ALIAS:?Set the signing alias.}"
: "${TOUSHIR_SIGNING_KEY_PASSWORD:?Set the signing password.}"
: "${JAVA_HOME:?Set JAVA_HOME to JDK 17.}"
: "${ANDROID_HOME:?Set ANDROID_HOME to the Android SDK.}"
export ANDROID_SDK_ROOT="$ANDROID_HOME"
export NODE_ENV=production
export EXPO_NO_TELEMETRY=1

cd "$TASK_ROOT/apps/mobile/android"
./gradlew assembleRelease --no-daemon --max-workers=2 -PreactNativeArchitectures=arm64-v8a
