import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive_flutter/hive_flutter.dart';

import 'providers/athlete_providers.dart';
import 'screens/home_screen.dart';
import 'services/athlete_profile_repository.dart';

/// App entry point.
///
/// Two responsibilities live here and nowhere else:
///   1. **Hive bootstrap** — open the athlete-profile box BEFORE the
///      first frame so [DigitalIdScreen] can do a synchronous cache
///      read. A loading spinner on the gate is not acceptable UX when
///      there's no internet.
///   2. **Riverpod overrides** — bind the concrete `apiBaseUrl` and
///      repository singleton into their provider handles so features
///      can keep declaring `throw UnimplementedError()` defaults that
///      fail loudly if wiring is missed.
void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await Hive.initFlutter();
  final profileRepo = await AthleteProfileRepository.open();

  runApp(
    ProviderScope(
      overrides: [
        apiBaseUrlProvider.overrideWithValue(SmfApp.apiBaseUrl),
        athleteProfileRepositoryProvider.overrideWithValue(profileRepo),
      ],
      child: const _AutoRefreshBootstrap(child: SmfApp()),
    ),
  );
}

/// Forces [autoRefreshDigitalIdProvider] to be read exactly once at
/// startup. Without this, the watcher is never instantiated (Riverpod
/// is lazy) and silent reconnect-triggered refreshes don't happen.
class _AutoRefreshBootstrap extends ConsumerWidget {
  const _AutoRefreshBootstrap({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(autoRefreshDigitalIdProvider);
    return child;
  }
}

class SmfApp extends StatelessWidget {
  const SmfApp({super.key});

  /// Resolution order for the API base URL:
  ///   1. `--dart-define=API_BASE_URL=…` (build flag)
  ///   2. default: Android-emulator host loopback on port 5000
  ///
  /// Physical devices on the same LAN should pass the dev machine's IP:
  ///   flutter run --dart-define=API_BASE_URL=http://192.168.1.42:5000
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000',
  );

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Saudi MuayThai Federation',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF006633)),
        useMaterial3: true,
      ),
      home: const HomeScreen(apiBaseUrl: apiBaseUrl),
    );
  }
}
