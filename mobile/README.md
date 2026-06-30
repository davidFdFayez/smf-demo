# SMF Referee Tablet (Flutter)

Flutter tablet app for MuayThai side/head referees. Talks to the
`MatchScoringHub` in `src/SMF.Api` over SignalR (WebSockets) and pushes
strikes with minimum latency.

## Layout

```
mobile/
├── pubspec.yaml
└── lib/
    ├── main.dart                          # landscape-locked app shell
    ├── models/scoring_events.dart         # FighterColor + payload DTOs
    ├── services/
    │   ├── auth_api_service.dart          # POST /api/auth/dev-token
    │   └── scoring_hub_service.dart       # SignalR client wrapper
    └── screens/
        ├── connect_screen.dart            # pick referee + match, auth
        └── referee_scoring_screen.dart    # two giant buttons + haptic
```

## Key design choices

- **`signalr_netcore` over the MessagePack protocol** — the server keeps JSON
  as a protocol option, so we use plain `HubConnectionBuilder` JSON. MessagePack
  adds binary tooling without a meaningful latency win for a single-byte strike.
- **JWT via `accessTokenFactory`** — SignalR puts the token on the WebSocket
  handshake query string. `JwtBearerEvents.OnMessageReceived` in the API
  promotes it to `Context.User`, which the hub uses to verify the caller is
  who they claim to be.
- **Fire-and-forget strike** — `ScoringHubService.fireStrike` dispatches the
  invocation and returns immediately. The UI fires `HapticFeedback.heavyImpact()`
  and bumps the local counter in the same frame. If the invoke fails, the
  error is surfaced via `lastError` on the hub service's `ChangeNotifier`
  and shown in the status bar — but subsequent taps are never blocked.
- **Landscape-only** — locked via `SystemChrome.setPreferredOrientations` in
  `main.dart`. The buttons arrange side-by-side in landscape, stacked in
  portrait (which shouldn't normally happen on a tablet).

## Running

```bash
# 1. Start the API. In another shell:
cd ../src/SMF.Api
dotnet run
# note the URL — usually http://localhost:5000

# 2. Run the Flutter app.
cd ../../mobile
flutter pub get

# Android emulator (uses host loopback 10.0.2.2 by default):
flutter run

# Physical tablet on LAN — pass the dev machine's IP:
flutter run --dart-define=API_BASE_URL=http://192.168.1.42:5000
```

The `Connect` screen defaults to `match-001` + `Side Referee A`, both of
which `SMF.Api.DevSeeder` creates automatically in the Development
environment. Tap `Connect & start scoring`, hit the big red/blue buttons, and
watch the web Scoreboard tab react in real time.

## CORS / cleartext note

- The API already allows `http://localhost:5173` (web dev server). If you run
  Flutter against the same API from a different origin (e.g. a LAN IP), add
  that origin to `Cors:AllowedOrigins` in `src/SMF.Api/appsettings.Development.json`.
- On Android, plaintext HTTP requires
  `android:usesCleartextTraffic="true"` in `AndroidManifest.xml` for dev.
  Use HTTPS in production.

## Production checklist

- [ ] Swap `/api/auth/dev-token` for the real IdP (Keycloak / Entra ID).
- [ ] Ship the JWT public key URL via `Jwt:Authority` instead of the shared
      signing-key secret used in Development.
- [ ] Pin the server cert (SSL pinning) for in-venue tablets.
- [ ] Enable `MessagePackHubProtocol` on the Flutter client too if you need
      every last byte — requires the `msgpack_dart` layer; not bundled here.
