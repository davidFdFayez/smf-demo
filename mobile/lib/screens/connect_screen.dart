import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../services/auth_api_service.dart';
import '../services/scoring_hub_service.dart';
import 'referee_scoring_screen.dart';

/// Tiny bootstrap screen – picks a seeded referee, grabs a dev JWT, connects
/// the hub, joins the match, and pushes the real scoring screen.
///
/// In production this gets replaced by a real IdP sign-in + match assignment
/// pull; the referee would never type a GUID.
class ConnectScreen extends StatefulWidget {
  const ConnectScreen({
    super.key,
    required this.apiBaseUrl,
  });

  final String apiBaseUrl;

  @override
  State<ConnectScreen> createState() => _ConnectScreenState();
}

class _SeededReferee {
  final String id;
  final String label;
  final bool isHead;
  const _SeededReferee(this.id, this.label, {this.isHead = false});
}

const _seededReferees = <_SeededReferee>[
  _SeededReferee("11111111-1111-1111-1111-111111111111", "Head Referee", isHead: true),
  _SeededReferee("22222222-2222-2222-2222-222222222222", "Side Referee A"),
  _SeededReferee("33333333-3333-3333-3333-333333333333", "Side Referee B"),
  _SeededReferee("44444444-4444-4444-4444-444444444444", "Side Referee C"),
];

class _ConnectScreenState extends State<ConnectScreen> {
  _SeededReferee _selected = _seededReferees[1];
  final _matchController = TextEditingController(text: "match-001");
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    // The referee tablet is used landscape — now that main.dart no
    // longer pins orientation globally (so the athlete flow can stay
    // portrait), each referee-side screen sets its own preference.
    SystemChrome.setPreferredOrientations(const [
      DeviceOrientation.landscapeLeft,
      DeviceOrientation.landscapeRight,
    ]);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  }

  @override
  void dispose() {
    _matchController.dispose();
    super.dispose();
  }

  Future<void> _connect() async {
    setState(() {
      _busy = true;
      _error = null;
    });

    final matchCode = _matchController.text.trim();
    if (matchCode.isEmpty) {
      setState(() {
        _busy = false;
        _error = "Match code is required.";
      });
      return;
    }

    final auth = AuthApiService(baseUrl: widget.apiBaseUrl);
    ScoringHubService? hub;
    try {
      final token = await auth.issueDevToken(
        refereeId: _selected.id,
        displayName: _selected.label,
        roles: _selected.isHead ? const ["HeadReferee"] : const ["Referee"],
      );

      hub = ScoringHubService(
        baseUrl: widget.apiBaseUrl,
        accessTokenFactory: () async => token.accessToken,
      );
      await hub.connect();
      await hub.joinMatch(matchCode);

      if (!mounted) {
        await hub.disconnect();
        hub.dispose();
        return;
      }

      await Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => RefereeScoringScreen(
            hub: hub!,
            matchCode: matchCode,
            refereeId: _selected.id,
            refereeLabel: _selected.label,
            isHeadReferee: _selected.isHead,
          ),
        ),
      );

      // When the referee pops off the scoring screen, tear down the hub.
      await hub.disconnect();
      hub.dispose();
    } catch (e) {
      await hub?.disconnect();
      hub?.dispose();
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      auth.dispose();
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("SMF Referee Tablet")),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 540),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                    "Select your identity",
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<_SeededReferee>(
                    value: _selected,
                    decoration: const InputDecoration(
                      border: OutlineInputBorder(),
                      labelText: "Referee",
                    ),
                    items: [
                      for (final r in _seededReferees)
                        DropdownMenuItem(
                          value: r,
                          child: Text("${r.label}${r.isHead ? ' (head)' : ''}"),
                        ),
                    ],
                    onChanged: _busy ? null : (v) => setState(() => _selected = v!),
                  ),
                  const SizedBox(height: 20),
                  TextField(
                    controller: _matchController,
                    enabled: !_busy,
                    decoration: const InputDecoration(
                      border: OutlineInputBorder(),
                      labelText: "Match code",
                      hintText: "match-001",
                    ),
                  ),
                  const SizedBox(height: 24),
                  if (_error != null)
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.red.shade50,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: Colors.red.shade200),
                      ),
                      child: Text(
                        _error!,
                        style: TextStyle(color: Colors.red.shade900),
                      ),
                    ),
                  if (_error != null) const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _busy ? null : _connect,
                    style: FilledButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
                    ),
                    child: _busy
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white),
                          )
                        : const Text("Connect & start scoring"),
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
