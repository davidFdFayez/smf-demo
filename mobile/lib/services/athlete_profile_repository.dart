import 'dart:async';

import 'package:hive/hive.dart';
import 'package:hive_flutter/hive_flutter.dart';

import '../models/athlete_profile.dart';

/// Hive-backed cache for the logged-in athlete's profile.
///
/// Storage shape: a single [`Box<String>`](https://docs.hivedb.dev/) where
/// the value is the profile serialised as JSON. Using JSON rather than a
/// generated TypeAdapter keeps the build toolchain lean (no
/// `build_runner`) and makes schema evolution a simple version bump
/// inside [AthleteProfile.toJson] / [AthleteProfile.fromJson].
///
/// Why Hive over SharedPreferences:
///   * Survives process restarts and OS memory pressure equally well.
///   * Synchronous read after `init()` means the digital-ID screen can
///     paint the cached QR on its very first frame — no loading spinner,
///     even offline.
///   * Built-in `listenable()` gives us a `ValueListenable` that feeds
///     Riverpod's StreamNotifier without polling.
///
/// The box is opened once during app startup (see `main.dart`) and kept
/// open for the lifetime of the process.
class AthleteProfileRepository {
  /// Hive box name. Namespaced under the app so it never collides with
  /// future boxes.
  static const String boxName = 'smf.athlete.profile.v1';

  /// Single well-known key — there's only one logged-in athlete at a
  /// time. Storing under a fixed key (rather than, say, the member id)
  /// simplifies lookup on cold start when we don't know the id yet.
  static const String _profileKey = 'current';

  final Box<String> _box;

  AthleteProfileRepository._(this._box);

  /// Opens the Hive box. Call once from `main.dart` AFTER
  /// `await Hive.initFlutter()`.
  static Future<AthleteProfileRepository> open() async {
    final box = await Hive.openBox<String>(boxName);
    return AthleteProfileRepository._(box);
  }

  /// Persist the profile. Idempotent — overwrites any prior value.
  Future<void> save(AthleteProfile profile) =>
      _box.put(_profileKey, profile.encode());

  /// Read the cached profile synchronously. Returns `null` on cold boot
  /// (no login yet) or when the payload is corrupt / from an older
  /// schema version.
  AthleteProfile? read() {
    final raw = _box.get(_profileKey);
    if (raw == null || raw.isEmpty) return null;
    return AthleteProfile.decode(raw);
  }

  /// Wipe the cache (logout). Safe to call when nothing is cached.
  Future<void> clear() => _box.delete(_profileKey);

  /// Emits the current profile, and a new value every time the cache
  /// changes. Useful for Riverpod `StreamProvider`s that want to react
  /// to save/clear without being told explicitly.
  Stream<AthleteProfile?> watch() async* {
    // Emit the current snapshot immediately so subscribers don't wait
    // for the next write to see anything.
    yield read();
    await for (final _ in _box.watch(key: _profileKey)) {
      yield read();
    }
  }

  /// Close the underlying box. Typically only used in tests —
  /// production apps leave the box open until process exit.
  Future<void> close() => _box.close();
}
