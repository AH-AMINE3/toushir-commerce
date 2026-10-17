# Mobile verification · 17 October 2026

The reports here contain sanitized evidence from the running .NET API and actual app interfaces. Credentials for isolated test actors stayed in process memory and the app's intended credential stores.

- `workflow-verification.json`: 10 connected browser checks, including RFQ, quote approval, pickup, quantity-bound partial receipt, separate cash/change, concurrent merchant/courier identities, messages, campaign preview and reward reservation.
- `oidc-ui-verification.json`: 3 actual Expo browser AuthSession checks: server login, S256 callback, API validation of the real driver JWT, rotating refresh restoration and no JavaScript runtime errors.
- `native-workflow-verification.json`: 6 Android Emulator API36 arm64 checks: real system-browser authorization, assigned pickup, consented Expo Location with an emulator-supplied GPS fix, manual receipt-code fallback, partial receipt and separate cash/change, SecureStore restoration after restarting the app.

`ios-verification.json` and the two iOS screenshots verify separate Release builds installed and running on iPhone 17 Pro / iOS 26.5. The merchant catalogue and courier manifest rendered from the API. iOS business interactions and system-browser PKCE were not executed because the native UI automation service could not start.

The GPS fix was supplied explicitly to the emulator; this is not evidence of a physical courier journey. Native receipt verification exercised the manual-code fallback. Physical camera scanning and real-device store distribution have not been tested by these reports.

The APKs in `build/downloads` currently target the local Android Emulator API at `http://10.0.2.2:5860`. Rebuild against a confirmed reachable HTTPS API before sharing them for remote testing. Browser previews use same-origin requests and the `/mobile/auth/callback` callback.
