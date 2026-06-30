import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../features/digital_id/i18n.dart';
import '../models/athlete_profile.dart';
import '../providers/athlete_providers.dart';
import 'athlete_login_screen.dart';

/// Whether the QR encodes the signed Digital ID token (business
/// default) or the bare SMF_ID (debug / local testing). Controlled at
/// build time via `--dart-define=QR_MODE=simple`.
///
/// Reason for the toggle: during development we often want to scan the
/// QR with a generic scanner (that can't hit /api/admissions/verify)
/// just to confirm the right value is displayed. In production the
/// default always wins.
enum QrMode { signedToken, rawSmfId }

const QrMode _qrMode = bool.fromEnvironment('QR_SIMPLE', defaultValue: false)
    ? QrMode.rawSmfId
    : QrMode.signedToken;

/// Digital accreditation card.
///
/// Contract:
///   * Reads the profile from the Hive-backed cache. Hive is opened at
///     app start, so this screen paints on its very first frame —
///     critical when the venue has no internet and the athlete needs
///     the QR in ~1s at the gate.
///   * The QR encodes a server-signed, time-bounded token (tamper-
///     resistant, forwarding-resistant within the expiry window).
///   * An "Offline Mode" banner appears whenever the device reports no
///     connectivity. The banner is purely informational — it NEVER
///     blocks the QR.
///   * A refresh button re-issues the token from the API when online;
///     it is disabled (not hidden) when offline so the affordance
///     stays stable.
class DigitalIdScreen extends ConsumerStatefulWidget {
  const DigitalIdScreen({super.key});

  @override
  ConsumerState<DigitalIdScreen> createState() => _DigitalIdScreenState();
}

class _DigitalIdScreenState extends ConsumerState<DigitalIdScreen>
    with WidgetsBindingObserver {
  Locale _locale = Locale.en;

  @override
  void initState() {
    super.initState();
    // Portrait + bright — gate staff need the QR readable at arm's
    // length. We don't force full brightness here (platform-specific)
    // but we do pin the orientation so the layout stays predictable.
    SystemChrome.setPreferredOrientations(const [
      DeviceOrientation.portraitUp,
      DeviceOrientation.portraitDown,
    ]);
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // On resume, attempt a silent refresh if the token is within the
    // expiring-soon window. Best-effort — never shows a spinner.
    if (state != AppLifecycleState.resumed) return;
    final cached = ref.read(cachedAthleteProfileProvider).valueOrNull;
    if (cached == null) return;
    final validity = cached.validityAt(DateTime.now().toUtc());
    if (validity == DigitalIdValidity.valid) return;
    ref
        .read(refreshProfileControllerProvider.notifier)
        .refresh(cached.id);
  }

  Future<void> _onRefresh(String memberId) async {
    final t = stringsFor(_locale);
    final ok = await ref
        .read(refreshProfileControllerProvider.notifier)
        .refresh(memberId);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(ok ? t.refreshSuccess : t.refreshFailed)),
    );
  }

  Future<void> _onLogout() async {
    await ref.read(athleteLoginControllerProvider.notifier).logout();
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => const AthleteLoginScreen()),
    );
  }

  void _toggleLocale() => setState(() {
        _locale = _locale == Locale.en ? Locale.ar : Locale.en;
      });

  @override
  Widget build(BuildContext context) {
    final cacheAsync = ref.watch(cachedAthleteProfileProvider);
    final onlineAsync = ref.watch(isOnlineProvider);
    final refreshState = ref.watch(refreshProfileControllerProvider);
    final t = stringsFor(_locale);
    final dir = directionFor(_locale);

    // `online` defaults to `true` before the first connectivity
    // reading arrives — showing a stale "Offline" banner during the
    // 50ms startup window would be misleading. The moment the stream
    // produces a real value we honor it.
    final online = onlineAsync.maybeWhen(
      data: (v) => v,
      orElse: () => true,
    );

    return Directionality(
      textDirection: dir,
      child: Scaffold(
        appBar: AppBar(
          title: Text(t.screenTitle),
          actions: [
            TextButton(
              onPressed: _toggleLocale,
              child: Text(
                _locale == Locale.en ? 'العربية' : 'English',
                style: const TextStyle(color: Colors.white),
              ),
            ),
            IconButton(
              tooltip: t.signOut,
              icon: const Icon(Icons.logout),
              onPressed: _onLogout,
            ),
          ],
        ),
        body: SafeArea(
          child: Column(
            children: [
              if (!online) _OfflineBanner(text: t.offlineBanner),
              Expanded(
                child: cacheAsync.when(
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (e, _) => _CenteredMessage(
                    icon: Icons.error_outline,
                    title: t.cacheError,
                    subtitle: '$e',
                  ),
                  data: (profile) {
                    if (profile == null) {
                      // No cache AND no login — bounce to login.
                      WidgetsBinding.instance.addPostFrameCallback((_) {
                        if (!mounted) return;
                        Navigator.of(context).pushReplacement(
                          MaterialPageRoute(
                            builder: (_) => const AthleteLoginScreen(),
                          ),
                        );
                      });
                      return const SizedBox.shrink();
                    }
                    return _IdCard(
                      profile: profile,
                      online: online,
                      refreshing: refreshState.isLoading,
                      strings: t,
                      onRefresh: () => _onRefresh(profile.id),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _OfflineBanner extends StatelessWidget {
  const _OfflineBanner({required this.text});
  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      color: Colors.amber.shade700,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      child: Row(
        children: [
          const Icon(Icons.wifi_off, color: Colors.white, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _IdCard extends StatelessWidget {
  const _IdCard({
    required this.profile,
    required this.online,
    required this.refreshing,
    required this.strings,
    required this.onRefresh,
  });

  final AthleteProfile profile;
  final bool online;
  final bool refreshing;
  final DigitalIdStrings strings;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    final cachedAt = profile.cachedAtUtc.toLocal();
    final theme = Theme.of(context);
    final validity = profile.validityAt(DateTime.now().toUtc());
    final qrPayload = _qrMode == QrMode.rawSmfId
        ? profile.smfId
        : profile.digitalIdToken;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Header card with name + SMF_ID + status + validity pill.
          Card(
            elevation: 2,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    profile.fullName,
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${strings.smfIdPrefix}${profile.smfId}',
                    style: theme.textTheme.bodyLarge
                        ?.copyWith(color: Colors.grey.shade700),
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _StatusPill(
                        status: profile.status,
                        label: strings.statusLabel(profile.status),
                      ),
                      _ValidityPill(validity: validity, strings: strings),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),

          // QR card — the actual entry credential.
          Card(
            elevation: 4,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 28, horizontal: 20),
              child: Column(
                children: [
                  Text(
                    strings.presentQrCaption,
                    style: theme.textTheme.titleMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  // White background with generous padding — QR readers
                  // struggle with glossy-dark backgrounds under
                  // fluorescent venue lighting.
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: Colors.grey.shade300),
                    ),
                    child: QrImageView(
                      data: qrPayload,
                      version: QrVersions.auto,
                      size: 260,
                      gapless: false,
                      // Quartile ECC — scans reliably even with a bit
                      // of screen glare or a cracked screen protector.
                      // With the full signed token the payload is
                      // ~220B so we stay well below version cap.
                      errorCorrectionLevel: QrErrorCorrectLevel.Q,
                      semanticsLabel: 'Digital ID QR for ${profile.smfId}',
                    ),
                  ),
                  const SizedBox(height: 12),
                  SelectableText(
                    profile.smfId,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      fontFamily: 'monospace',
                      letterSpacing: 1.2,
                    ),
                  ),
                  if (validity == DigitalIdValidity.expired) ...[
                    const SizedBox(height: 16),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.red.shade50,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.red.shade200),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.error_outline,
                            color: Colors.red.shade700,
                            size: 20,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              strings.tokenExpiredInstruction,
                              style: TextStyle(
                                color: Colors.red.shade800,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),

          // Footer with cache timestamp and refresh affordance. The
          // refresh button is disabled (not hidden) when offline so
          // users learn its position.
          Row(
            children: [
              Icon(
                online ? Icons.cloud_done_outlined : Icons.cloud_off_outlined,
                size: 18,
                color: Colors.grey.shade700,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  '${strings.lastSynced} ${_formatTimestamp(cachedAt)}',
                  style: TextStyle(color: Colors.grey.shade700, fontSize: 12),
                ),
              ),
              TextButton.icon(
                onPressed: (online && !refreshing) ? onRefresh : null,
                icon: refreshing
                    ? const SizedBox(
                        height: 14,
                        width: 14,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.refresh, size: 18),
                label: Text(refreshing ? strings.refreshing : strings.refresh),
              ),
            ],
          ),
        ],
      ),
    );
  }

  static String _formatTimestamp(DateTime local) {
    String two(int n) => n.toString().padLeft(2, '0');
    return '${local.year}-${two(local.month)}-${two(local.day)} '
        '${two(local.hour)}:${two(local.minute)}';
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.status, required this.label});
  final RegistrationStatus status;
  final String label;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (status) {
      RegistrationStatus.active => (Colors.green.shade50, Colors.green.shade800),
      RegistrationStatus.approved => (Colors.blue.shade50, Colors.blue.shade800),
      RegistrationStatus.pending => (Colors.orange.shade50, Colors.orange.shade800),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: fg.withOpacity(0.3)),
      ),
      child: Text(
        label,
        style: TextStyle(color: fg, fontWeight: FontWeight.w600),
      ),
    );
  }
}

class _ValidityPill extends StatelessWidget {
  const _ValidityPill({required this.validity, required this.strings});
  final DigitalIdValidity validity;
  final DigitalIdStrings strings;

  @override
  Widget build(BuildContext context) {
    final (bg, fg, icon, label) = switch (validity) {
      DigitalIdValidity.valid => (
          Colors.green.shade50,
          Colors.green.shade800,
          Icons.verified_outlined,
          strings.validPill,
        ),
      DigitalIdValidity.expiringSoon => (
          Colors.amber.shade50,
          Colors.amber.shade900,
          Icons.timelapse,
          strings.expiringSoonPill,
        ),
      DigitalIdValidity.expired => (
          Colors.red.shade50,
          Colors.red.shade800,
          Icons.error_outline,
          strings.expiredPill,
        ),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: fg.withOpacity(0.3)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, color: fg, size: 14),
          const SizedBox(width: 6),
          Text(label, style: TextStyle(color: fg, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

class _CenteredMessage extends StatelessWidget {
  const _CenteredMessage({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 48, color: Colors.grey.shade500),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(
              subtitle,
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey.shade700),
            ),
          ],
        ),
      ),
    );
  }
}
