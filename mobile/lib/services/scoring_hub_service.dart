import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../models/scoring_events.dart';

/// High-level SignalR client around `MatchScoringHub`.
///
/// Responsibilities:
///   * Build + start a `HubConnection` against `/hubs/match-scoring`
///   * Authenticate with a JWT via `accessTokenFactory` (SignalR will pass it
///     as `?access_token=…` on the WebSocket handshake — which the API's
///     `JwtBearerEvents.OnMessageReceived` extracts)
///   * Join / leave a match's broadcast group
///   * Invoke `SubmitStrike` with minimum latency (non-awaiting wrapper
///     available so the UI can return to haptic feedback immediately)
///   * Surface `ReceiveStrikeUpdate` / `ReceiveScoreOverride` as Dart streams
class ScoringHubService extends ChangeNotifier {
  ScoringHubService({
    required this.baseUrl,
    required this.accessTokenFactory,
  });

  /// e.g. `http://10.0.2.2:5000` for Android emulator → host, or
  /// `http://<lan-ip>:5000` for a physical tablet.
  final String baseUrl;

  /// Called by SignalR every time it needs a fresh bearer token — e.g. on
  /// the initial connect and after any reconnect.
  final Future<String> Function() accessTokenFactory;

  HubConnection? _connection;
  String? _currentMatchCode;
  String? _lastError;
  TimerStateUpdate? _timer;
  int _remoteRed = 0;
  int _remoteBlue = 0;

  final StreamController<StrikeUpdate> _strikes =
      StreamController<StrikeUpdate>.broadcast();
  final StreamController<ScoreOverrideUpdate> _overrides =
      StreamController<ScoreOverrideUpdate>.broadcast();
  final StreamController<TimerStateUpdate> _timerUpdates =
      StreamController<TimerStateUpdate>.broadcast();

  Stream<StrikeUpdate> get strikeUpdates => _strikes.stream;
  Stream<ScoreOverrideUpdate> get scoreOverrides => _overrides.stream;
  Stream<TimerStateUpdate> get timerUpdates => _timerUpdates.stream;

  HubConnectionState get state =>
      _connection?.state ?? HubConnectionState.Disconnected;
  bool get isConnected => state == HubConnectionState.Connected;
  String? get currentMatchCode => _currentMatchCode;
  String? get lastError => _lastError;

  /// The most recent authoritative timer snapshot, or null if no update has
  /// arrived yet.
  TimerStateUpdate? get timer => _timer;

  /// Running score as derived from every broadcast strike + override
  /// (replayed in timestamp order). The head-ref dashboard uses this to
  /// pre-fill the override dialog.
  int get remoteRed => _remoteRed;
  int get remoteBlue => _remoteBlue;

  // ───────────────────────────────────────── lifecycle ──────────────────────

  Future<void> connect() async {
    await disconnect();

    final hubUrl = "${_trimTrailingSlash(baseUrl)}/hubs/match-scoring";

    final options = HttpConnectionOptions(
      accessTokenFactory: accessTokenFactory,
      // WebSockets gives the lowest-latency bidirectional channel; long
      // polling is the fallback if the negotiation selects it.
      transport: HttpTransportType.WebSockets,
      logMessageContent: false,
    );

    final connection = HubConnectionBuilder()
        .withUrl(hubUrl, options: options)
        .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 20000])
        .build();

    connection.on("ReceiveStrikeUpdate", _handleStrikeUpdate);
    connection.on("ReceiveScoreOverride", _handleScoreOverride);
    connection.on("ReceiveTimerUpdate", _handleTimerUpdate);

    connection.onclose(({Exception? error}) {
      _lastError = error?.toString();
      notifyListeners();
    });
    connection.onreconnecting(({Exception? error}) {
      _lastError = error?.toString();
      notifyListeners();
    });
    connection.onreconnected(({String? connectionId}) async {
      // Silently re-join the group after the transport came back, so the
      // referee keeps receiving broadcasts without manual intervention.
      final code = _currentMatchCode;
      if (code != null) {
        try {
          await connection.invoke("JoinMatch", args: [code]);
          // Re-pull the authoritative clock so the local clock catches up
          // to any round transitions that happened during the blip.
          await connection.invoke("RequestTimerState", args: [code]);
        } catch (e) {
          _lastError = e.toString();
        }
      }
      notifyListeners();
    });

    await connection.start();
    _connection = connection;
    _lastError = null;
    notifyListeners();
  }

  Future<void> disconnect() async {
    final existing = _connection;
    _connection = null;
    _currentMatchCode = null;
    if (existing != null) {
      try {
        await existing.stop();
      } catch (_) {
        // Swallow — we're tearing down anyway.
      }
    }
    notifyListeners();
  }

  // ───────────────────────────────────────── match ──────────────────────────

  Future<void> joinMatch(String matchCode) async {
    final conn = _requireConnection();
    await conn.invoke("JoinMatch", args: [matchCode]);
    _currentMatchCode = matchCode;
    // Reset derived state so switching matches doesn't leak the old score.
    _remoteRed = 0;
    _remoteBlue = 0;
    _timer = null;
    notifyListeners();

    // Ask the server for the current timer snapshot – without this the UI
    // would show "--:--" until the next round transition.
    try {
      await conn.invoke("RequestTimerState", args: [matchCode]);
    } catch (_) {
      // Best-effort; not worth blocking the join on.
    }
  }

  Future<void> leaveMatch() async {
    final conn = _connection;
    final code = _currentMatchCode;
    if (conn == null || code == null) return;
    try {
      await conn.invoke("LeaveMatch", args: [code]);
    } catch (_) {
      // Ignore — user-initiated.
    }
    _currentMatchCode = null;
    notifyListeners();
  }

  // ───────────────────────────────────────── strike ─────────────────────────

  /// Fire a strike. Returns the invocation future so callers can await the
  /// server ack — but the UI layer should usually call [fireStrike] instead
  /// to give haptic feedback immediately and let the network call complete
  /// in the background.
  Future<void> submitStrike({
    required String matchCode,
    required String refereeId,
    required FighterColor fighterColor,
  }) async {
    final conn = _requireConnection();
    await conn.invoke(
      "SubmitStrike",
      args: [matchCode, refereeId, fighterColor.wireValue],
    );
  }

  /// Fire-and-forget strike wrapper. Returns immediately after the invocation
  /// is dispatched so the UI can play a haptic and animate without waiting
  /// for the server ack. Errors are emitted via [lastError].
  void fireStrike({
    required String matchCode,
    required String refereeId,
    required FighterColor fighterColor,
  }) {
    // Capture the connection now so a disconnect mid-flight doesn't NPE.
    final conn = _connection;
    if (conn == null || conn.state != HubConnectionState.Connected) {
      _lastError = "Not connected to scoring hub.";
      notifyListeners();
      return;
    }
    conn
        .invoke(
          "SubmitStrike",
          args: [matchCode, refereeId, fighterColor.wireValue],
        )
        .then((_) {
      if (_lastError != null) {
        _lastError = null;
        notifyListeners();
      }
    }).catchError((Object error) {
      _lastError = error.toString();
      notifyListeners();
    });
  }

  // ───────────────────────────────────────── override (head ref only) ───────

  Future<void> overrideScore({
    required String matchCode,
    required String headRefereeId,
    required int red,
    required int blue,
    int? round,
  }) async {
    final conn = _requireConnection();
    await conn.invoke("OverrideScore", args: [
      matchCode,
      headRefereeId,
      <String, dynamic>{"red": red, "blue": blue, "round": round},
    ]);
  }

  /// Head-referee convenience: push an override that subtracts one point
  /// from the last recorded strike's fighter. Returns true if a strike was
  /// available to nullify.
  Future<bool> nullifyLastStrike({
    required String matchCode,
    required String headRefereeId,
  }) async {
    if (_lastStrikeColor == null) return false;
    final int newRed =
        _lastStrikeColor == FighterColor.red ? (_remoteRed > 0 ? _remoteRed - 1 : 0) : _remoteRed;
    final int newBlue =
        _lastStrikeColor == FighterColor.blue ? (_remoteBlue > 0 ? _remoteBlue - 1 : 0) : _remoteBlue;
    await overrideScore(
      matchCode: matchCode,
      headRefereeId: headRefereeId,
      red: newRed,
      blue: newBlue,
      round: _timer?.currentRound,
    );
    return true;
  }

  /// Request a fresh timer snapshot from the server (e.g. after a reconnect
  /// or when the UI needs to refresh a stale clock).
  Future<void> requestTimerState(String matchCode) async {
    final conn = _requireConnection();
    await conn.invoke("RequestTimerState", args: [matchCode]);
  }

  // ───────────────────────────────────────── internals ──────────────────────

  HubConnection _requireConnection() {
    final conn = _connection;
    if (conn == null || conn.state != HubConnectionState.Connected) {
      throw StateError("Scoring hub is not connected.");
    }
    return conn;
  }

  FighterColor? _lastStrikeColor;

  void _handleStrikeUpdate(List<Object?>? args) {
    final payload = _firstMap(args);
    if (payload == null) return;
    final strike = StrikeUpdate.fromJson(payload);
    // Update the authoritative running score from broadcast events. We use
    // this instead of the local referee-taps counter because overrides can
    // rewrite the score without the tap counter knowing.
    if (strike.fighterColor == FighterColor.red) {
      _remoteRed += 1;
    } else {
      _remoteBlue += 1;
    }
    _lastStrikeColor = strike.fighterColor;
    _strikes.add(strike);
    notifyListeners();
  }

  void _handleScoreOverride(List<Object?>? args) {
    final payload = _firstMap(args);
    if (payload == null) return;
    final override = ScoreOverrideUpdate.fromJson(payload);
    _remoteRed = override.red;
    _remoteBlue = override.blue;
    _overrides.add(override);
    notifyListeners();
  }

  void _handleTimerUpdate(List<Object?>? args) {
    final payload = _firstMap(args);
    if (payload == null) return;
    final update = TimerStateUpdate.fromJson(payload);
    _timer = update;
    _timerUpdates.add(update);
    notifyListeners();
  }

  Map<String, dynamic>? _firstMap(List<Object?>? args) {
    if (args == null || args.isEmpty) return null;
    final raw = args.first;
    if (raw is Map<String, dynamic>) return raw;
    if (raw is Map) return Map<String, dynamic>.from(raw);
    return null;
  }

  static String _trimTrailingSlash(String url) =>
      url.endsWith("/") ? url.substring(0, url.length - 1) : url;

  @override
  void dispose() {
    _strikes.close();
    _overrides.close();
    _timerUpdates.close();
    unawaited(_connection?.stop());
    super.dispose();
  }
}
