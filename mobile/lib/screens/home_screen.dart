import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/athlete_providers.dart';
import 'athlete_login_screen.dart';
import 'connect_screen.dart';
import 'digital_id_screen.dart';

/// Landing screen that routes to either the referee or athlete flow.
///
/// If an athlete profile is already cached we surface a "Continue as
/// <name>" shortcut so the QR is always one tap away at the venue gate.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key, required this.apiBaseUrl});

  final String apiBaseUrl;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cacheAsync = ref.watch(cachedAthleteProfileProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Saudi MuayThai Federation')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 24),
              const Text(
                'Who are you signing in as?',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 32),

              cacheAsync.maybeWhen(
                data: (profile) {
                  if (profile == null) return const SizedBox.shrink();
                  return Column(
                    children: [
                      FilledButton.tonalIcon(
                        onPressed: () => Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => const DigitalIdScreen(),
                          ),
                        ),
                        icon: const Icon(Icons.qr_code_2),
                        label: Text('Continue as ${profile.fullName}'),
                        style: FilledButton.styleFrom(
                          padding: const EdgeInsets.symmetric(vertical: 16),
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],
                  );
                },
                orElse: () => const SizedBox.shrink(),
              ),

              FilledButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => const AthleteLoginScreen(),
                  ),
                ),
                icon: const Icon(Icons.badge_outlined),
                label: const Text('Athlete — Digital ID'),
                style: FilledButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
              ),
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => ConnectScreen(apiBaseUrl: apiBaseUrl),
                  ),
                ),
                icon: const Icon(Icons.sports_mma_outlined),
                label: const Text('Referee — Match scoring'),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
