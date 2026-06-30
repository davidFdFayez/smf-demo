import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';

/// Abstract interface so tests and alternative platforms can plug in a
/// fake without pulling the `connectivity_plus` plugin into the unit
/// test runner.
abstract class ConnectivityService {
  /// Fires an event every time connectivity changes. The first value
  /// is the current snapshot so consumers don't have to await a change
  /// before showing the correct UI.
  Stream<bool> get onlineStream;

  /// One-shot current reading. Avoid in widget builds (use the stream);
  /// useful on button handlers that want a fresh check before retrying
  /// an API call.
  Future<bool> isOnline();
}

/// Wrapper around [Connectivity] that normalises the raw result list
/// into a single boolean:
///   * at least one non-`none` transport  → online = true
///   * only `none` / empty                → online = false
///
/// `connectivity_plus` v6 returns a *list* because a device can have
/// several active transports at once (wifi + vpn + ethernet). We don't
/// need that granularity — the digital-ID screen only asks "can the
/// refresh button do anything?".
class ConnectivityPlusService implements ConnectivityService {
  final Connectivity _connectivity;
  final StreamController<bool> _controller = StreamController<bool>.broadcast();
  StreamSubscription<List<ConnectivityResult>>? _sub;
  bool? _last;

  ConnectivityPlusService({Connectivity? connectivity})
      : _connectivity = connectivity ?? Connectivity() {
    _start();
  }

  void _start() {
    // Prime the stream with a current reading so the first subscriber
    // sees the right state on frame 1.
    _connectivity.checkConnectivity().then((result) {
      final online = _isAnyOnline(result);
      _last = online;
      _controller.add(online);
    }).catchError((_) {
      _last = false;
      _controller.add(false);
    });

    _sub = _connectivity.onConnectivityChanged.listen((result) {
      final online = _isAnyOnline(result);
      if (online != _last) {
        _last = online;
        _controller.add(online);
      }
    });
  }

  static bool _isAnyOnline(List<ConnectivityResult> result) {
    if (result.isEmpty) return false;
    return result.any((r) => r != ConnectivityResult.none);
  }

  @override
  Stream<bool> get onlineStream => _controller.stream;

  @override
  Future<bool> isOnline() async {
    final r = await _connectivity.checkConnectivity();
    return _isAnyOnline(r);
  }

  Future<void> dispose() async {
    await _sub?.cancel();
    await _controller.close();
  }
}
