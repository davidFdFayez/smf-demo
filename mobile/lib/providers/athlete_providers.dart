import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/athlete_profile.dart';
import '../services/athlete_api_service.dart';
import '../services/athlete_profile_repository.dart';
import '../services/connectivity_service.dart';

/// API base URL — injected at app startup via `overrideWithValue`.
/// Providers that depend on it throw if it wasn't overridden, which is
/// a deliberately loud failure mode (we never want to silently hit the
/// wrong environment).
final apiBaseUrlProvider = Provider<String>((ref) {
  throw UnimplementedError(
    'apiBaseUrlProvider was not overridden — wire it up in main.dart.',
  );
});

/// The open Hive box wrapper. Overridden at startup with the already-
/// opened repository so providers never have to await `Hive.openBox`.
final athleteProfileRepositoryProvider =
    Provider<AthleteProfileRepository>((ref) {
  throw UnimplementedError(
    'athleteProfileRepositoryProvider was not overridden — '
    'call AthleteProfileRepository.open() in main.dart and override it.',
  );
});

final athleteApiServiceProvider = Provider<AthleteApiService>((ref) {
  final service = AthleteApiService(baseUrl: ref.watch(apiBaseUrlProvider));
  ref.onDispose(service.dispose);
  return service;
});

final connectivityServiceProvider = Provider<ConnectivityService>((ref) {
  final service = ConnectivityPlusService();
  ref.onDispose(service.dispose);
  return service;
});

/// Emits `true` while the device has at least one non-none transport.
/// Used by the DigitalIdScreen to render the "Offline Mode" banner
/// without blocking the QR render (cache always wins).
final isOnlineProvider = StreamProvider<bool>((ref) {
  final svc = ref.watch(connectivityServiceProvider);
  return svc.onlineStream;
});

/// Cached profile, fed by [AthleteProfileRepository.watch]. First value
/// is the current cache snapshot (may be null on cold boot / after
/// logout), subsequent values fire on save / clear.
final cachedAthleteProfileProvider =
    StreamProvider<AthleteProfile?>((ref) {
  final repo = ref.watch(athleteProfileRepositoryProvider);
  return repo.watch();
});

// ─────────────────────────────────────────── Login ───────────────────────────────────

/// Result of attempting a login. Kept narrow on purpose.
sealed class AthleteLoginState {
  const AthleteLoginState();
}

class AthleteLoginIdle extends AthleteLoginState {
  const AthleteLoginIdle();
}

class AthleteLoginInProgress extends AthleteLoginState {
  const AthleteLoginInProgress();
}

class AthleteLoginSuccess extends AthleteLoginState {
  final AthleteProfile profile;
  const AthleteLoginSuccess(this.profile);
}

class AthleteLoginFailure extends AthleteLoginState {
  final String message;
  const AthleteLoginFailure(this.message);
}

/// Riverpod notifier that drives the AthleteLoginScreen. On success we
/// persist the profile to Hive BEFORE transitioning state, so the
/// digital-ID screen will find it in cache the moment it builds —
/// meaning the venue QR survives a mid-flight process kill.
class AthleteLoginController extends Notifier<AthleteLoginState> {
  @override
  AthleteLoginState build() => const AthleteLoginIdle();

  Future<void> login(String memberId) async {
    final trimmed = memberId.trim();
    if (trimmed.isEmpty) {
      state = const AthleteLoginFailure('Member id is required.');
      return;
    }

    state = const AthleteLoginInProgress();

    final api = ref.read(athleteApiServiceProvider);
    final repo = ref.read(athleteProfileRepositoryProvider);

    try {
      final profile = await api.issueDigitalId(trimmed);

      // Persist to Hive BEFORE we transition to Success. If a crash
      // lands between the await and the state mutation, the next app
      // launch still finds the profile in cache — so the athlete's QR
      // is available offline as promised.
      await repo.save(profile);

      state = AthleteLoginSuccess(profile);
    } on AthleteNotFoundException {
      state = const AthleteLoginFailure(
        'That member id could not be found. Please check and try again.',
      );
    } on AthleteNetworkException {
      state = const AthleteLoginFailure(
        'You appear to be offline. Connect once to fetch your profile; '
        'afterwards it is available offline.',
      );
    } on AthleteApiException catch (e) {
      state = AthleteLoginFailure('Server error (${e.statusCode}).');
    } catch (e) {
      state = AthleteLoginFailure('Unexpected error: $e');
    }
  }

  /// Wipe the cache so the next DigitalIdScreen build sees `null` and
  /// the user is routed back to the login screen.
  Future<void> logout() async {
    final repo = ref.read(athleteProfileRepositoryProvider);
    await repo.clear();
    state = const AthleteLoginIdle();
  }

  void reset() {
    state = const AthleteLoginIdle();
  }
}

final athleteLoginControllerProvider =
    NotifierProvider<AthleteLoginController, AthleteLoginState>(
  AthleteLoginController.new,
);

// ─────────────────────────────────────────── Refresh ─────────────────────────────────

/// Refresh action for the DigitalIdScreen. Best-effort: if we're
/// offline or the server is unreachable, the cached profile is
/// untouched and the UI stays functional.
class RefreshProfileController extends Notifier<AsyncValue<void>> {
  @override
  AsyncValue<void> build() => const AsyncValue.data(null);

  Future<bool> refresh(String memberId) async {
    state = const AsyncValue.loading();
    final api = ref.read(athleteApiServiceProvider);
    final repo = ref.read(athleteProfileRepositoryProvider);
    try {
      final profile = await api.issueDigitalId(memberId);
      await repo.save(profile);
      state = const AsyncValue.data(null);
      return true;
    } on AthleteNetworkException catch (e, st) {
      // Offline refresh is non-fatal — emit the error to the UI but
      // leave the cached profile alone.
      state = AsyncValue.error(e, st);
      return false;
    } catch (e, st) {
      state = AsyncValue.error(e, st);
      return false;
    }
  }
}

final refreshProfileControllerProvider =
    NotifierProvider<RefreshProfileController, AsyncValue<void>>(
  RefreshProfileController.new,
);

// ─────────────────────────────── Auto-refresh on reconnect ──────────────────────────

/// Background observer that silently re-issues the Digital ID token
/// whenever connectivity transitions from offline → online AND the
/// current cached token is within its expiring-soon window (or already
/// expired). Prevents athletes from ever arriving at the gate with a
/// stale-but-salvageable token.
///
/// No UI; started by `main.dart` via `ref.read(autoRefreshDigitalIdProvider)`.
final autoRefreshDigitalIdProvider = Provider<_AutoRefreshWatcher>((ref) {
  final watcher = _AutoRefreshWatcher(ref);
  ref.onDispose(watcher.dispose);
  return watcher;
});

class _AutoRefreshWatcher {
  final Ref _ref;
  bool? _wasOnline;
  bool _refreshInFlight = false;

  _AutoRefreshWatcher(this._ref) {
    // We can't use `ref.listen` outside a widget tree, but StreamProvider
    // values propagate through ref.watch inside *another* provider. The
    // simplest dependency-free approach: subscribe directly to the
    // connectivity stream and react.
    final svc = _ref.read(connectivityServiceProvider);
    _sub = svc.onlineStream.listen(_onConnectivity);
  }

  late final StreamSubscription<bool> _sub;

  Future<void> _onConnectivity(bool online) async {
    final previous = _wasOnline;
    _wasOnline = online;

    // Only act on the offline → online edge. Boot-time reading never
    // triggers a refresh; that's the Digital ID screen's explicit
    // "Refresh" button's job.
    if (previous != false || !online) return;
    if (_refreshInFlight) return;

    final cached = _ref.read(cachedAthleteProfileProvider).valueOrNull;
    if (cached == null) return;

    final validity = cached.validityAt(DateTime.now().toUtc());
    if (validity == DigitalIdValidity.valid) return;

    _refreshInFlight = true;
    try {
      await _ref.read(refreshProfileControllerProvider.notifier).refresh(cached.id);
    } finally {
      _refreshInFlight = false;
    }
  }

  void dispose() => _sub.cancel();
}
