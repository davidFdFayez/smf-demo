import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/digital_id/i18n.dart';
import '../providers/athlete_providers.dart';
import 'digital_id_screen.dart';

/// Minimal athlete login screen.
///
/// Flow:
///   1. User enters their member id (GUID).
///   2. We call `POST /api/members/{id}/digital-id` to get a signed token.
///   3. On success the [AthleteLoginController] persists the profile +
///      token to Hive and flips state to [AthleteLoginSuccess]; we
///      then navigate to [DigitalIdScreen].
///
/// This screen is intentionally paper-thin — in production it gets
/// replaced by an IdP-driven SSO flow. Everything downstream (the cache,
/// the digital-ID screen, offline rendering) is agnostic to how the
/// profile arrived.
class AthleteLoginScreen extends ConsumerStatefulWidget {
  const AthleteLoginScreen({super.key});

  @override
  ConsumerState<AthleteLoginScreen> createState() => _AthleteLoginScreenState();
}

class _AthleteLoginScreenState extends ConsumerState<AthleteLoginScreen> {
  final _idController = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  Locale _locale = Locale.en;

  @override
  void initState() {
    super.initState();
    // Portrait layout suits a phone-in-hand login; the referee flow
    // locks landscape from its own initState.
    SystemChrome.setPreferredOrientations(const [
      DeviceOrientation.portraitUp,
      DeviceOrientation.portraitDown,
    ]);
  }

  @override
  void dispose() {
    _idController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    await ref
        .read(athleteLoginControllerProvider.notifier)
        .login(_idController.text);
  }

  void _toggleLocale() => setState(() {
        _locale = _locale == Locale.en ? Locale.ar : Locale.en;
      });

  @override
  Widget build(BuildContext context) {
    final t = stringsFor(_locale);
    final dir = directionFor(_locale);

    // If a profile is already cached, skip straight to the digital ID.
    // Listening here rather than in `initState` keeps navigation
    // reactive — a save from anywhere pushes the user forward.
    ref.listen<AsyncValue<dynamic>>(cachedAthleteProfileProvider, (prev, next) {
      next.whenData((profile) {
        if (profile != null && mounted) {
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(builder: (_) => const DigitalIdScreen()),
          );
        }
      });
    });

    // React to login state transitions — navigate on success, show a
    // snackbar on failure.
    ref.listen<AthleteLoginState>(athleteLoginControllerProvider, (prev, next) {
      if (!mounted) return;
      switch (next) {
        case AthleteLoginSuccess():
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(builder: (_) => const DigitalIdScreen()),
          );
          break;
        case AthleteLoginFailure(:final message):
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(message)),
          );
          break;
        case AthleteLoginIdle():
        case AthleteLoginInProgress():
          break;
      }
    });

    final login = ref.watch(athleteLoginControllerProvider);
    final busy = login is AthleteLoginInProgress;

    return Directionality(
      textDirection: dir,
      child: Scaffold(
        appBar: AppBar(
          title: Text(t.loginTitle),
          actions: [
            TextButton(
              onPressed: _toggleLocale,
              child: Text(
                _locale == Locale.en ? 'العربية' : 'English',
                style: const TextStyle(color: Colors.white),
              ),
            ),
          ],
        ),
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 16),
                  Text(
                    t.loginTitle,
                    style: const TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    t.loginBlurb,
                    style: TextStyle(color: Colors.grey.shade700),
                  ),
                  const SizedBox(height: 32),
                  TextFormField(
                    controller: _idController,
                    autofocus: true,
                    enabled: !busy,
                    decoration: InputDecoration(
                      labelText: t.memberIdLabel,
                      hintText: t.memberIdHint,
                      border: const OutlineInputBorder(),
                    ),
                    validator: (value) {
                      final v = value?.trim() ?? '';
                      if (v.isEmpty) return t.memberIdRequired;
                      if (!RegExp(
                        r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-'
                        r'[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
                      ).hasMatch(v)) {
                        return t.memberIdInvalid;
                      }
                      return null;
                    },
                  ),
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: busy ? null : _submit,
                    style: FilledButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                    ),
                    child: busy
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Text(t.signInCta),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
